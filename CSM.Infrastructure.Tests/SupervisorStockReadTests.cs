using System.Reflection;
using System.Security.Claims;
using CSM.Api.Controllers;
using CSM.Application.Common.Exceptions;
using CSM.Application.Common.Security;
using CSM.Application.Inventory.StockBalances;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Entities.Procurement;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Security;
using CSM.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class SupervisorStockReadTests
{
    [Fact]
    public async Task Supervisor_reads_only_active_assigned_sites_in_own_company_in_list_and_detail()
    {
        using var db = Database();
        var company = Guid.NewGuid();
        var actor = new User { Id = Guid.NewGuid(), CompanyId = company, IsActive = true,
            UserRoles = [new UserRole { Role = new Role { Name = AppRoles.Supervisor, NormalizedName = "SUPERVISOR" } }] };
        db.Users.Add(actor);
        var allowed = Stock(company, "Assigned");
        var unassigned = Stock(company, "Unassigned");
        var inactive = Stock(company, "Inactive access");
        var inactiveSite = Stock(company, "Inactive site");
        inactiveSite.ConstructionSite.IsActive = false;
        var foreign = Stock(Guid.NewGuid(), "Foreign");
        db.StockItems.AddRange(allowed, unassigned, inactive, inactiveSite, foreign);
        foreach (var stock in new[] { allowed, inactive, inactiveSite, foreign })
            db.UserSiteAccesses.Add(new UserSiteAccess { UserId = actor.Id, User = actor,
                SiteId = stock.ConstructionSiteId, Site = stock.ConstructionSite,
                CompanyId = company, IsActive = stock != inactive });
        db.SaveChanges(acceptAllChangesOnSuccess: true);
        var service = new StockBalanceService(db);
        Assert.Equal(allowed.Id, Assert.Single(await service.GetAllAsync(actor.Id)).StockItemId);
        Assert.Equal(6m, (await service.GetByIdAsync(actor.Id, allowed.Id)).QuantityOnHand);
        foreach (var denied in new[] { unassigned, inactive, inactiveSite, foreign })
        {
            Assert.Empty(await service.GetAllAsync(actor.Id, denied.ConstructionSiteId));
            await Assert.ThrowsAsync<StockBalanceManagementException>(() => service.GetByIdAsync(actor.Id, denied.Id));
        }
        actor.IsActive = false;
        db.SaveChanges(acceptAllChangesOnSuccess: true);
        await Assert.ThrowsAsync<StockBalanceManagementException>(() => service.GetAllAsync(actor.Id));
    }

    [Fact]
    public async Task Seeded_Supervisor_can_read_balances_but_all_stock_mutation_endpoints_are_denied()
    {
        using var db = Database();
        db.Users.Add(new User { Id = Guid.NewGuid(), IsActive = true,
            UserRoles = [new UserRole { Role = new Role { Name = AppRoles.SuperAdmin, NormalizedName = "SUPERADMIN" } }] });
        db.SaveChanges(acceptAllChangesOnSuccess: true);
        using var seedProvider = new ServiceCollection().AddSingleton<ApplicationDbContext>(db)
            .AddSingleton<IPasswordHasher, Hasher>().BuildServiceProvider();
        await DatabaseInitializer.InitializeAsync(seedProvider, new ConfigurationBuilder().Build());
        var role = db.Roles.Single(x => x.NormalizedName == "SUPERVISOR");
        var actorId = Guid.NewGuid();
        db.Users.Add(new User { Id = actorId, IsActive = true, CompanyId = Guid.NewGuid(),
            UserRoles = [new UserRole { RoleId = role.Id, Role = role }] });
        db.SaveChanges(acceptAllChangesOnSuccess: true);
        var evaluator = new PermissionEvaluator(db);
        Assert.True(await evaluator.HasPermissionAsync(actorId, GranularPermission.Procurement_Inventory_View.ToString()));
        Assert.False(await evaluator.HasPermissionAsync(actorId, GranularPermission.Procurement_Inventory_Adjust.ToString()));
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton<IPermissionEvaluator>(evaluator);
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddAuthorization(o =>
        {
            o.AddPolicy("Procurement.Inventory.View", p => p.AddRequirements(new PermissionRequirement(GranularPermission.Procurement_Inventory_View.ToString())));
            o.AddPolicy("Procurement.Inventory.Adjust", p => p.AddRequirements(new PermissionRequirement(GranularPermission.Procurement_Inventory_Adjust.ToString())));
        });
        using var provider = services.BuildServiceProvider();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, actorId.ToString()), new Claim(ClaimTypes.Role, AppRoles.Supervisor)], "test"));
        Assert.True(await Allowed(typeof(StockBalancesController), "GetAll"));
        Assert.True(await Allowed(typeof(StockBalancesController), "GetById"));
        foreach (var controller in new[] { typeof(StockIssuesController), typeof(StockReturnsController),
            typeof(StockTransfersController), typeof(StockAdjustmentsController) })
            Assert.False(await Allowed(controller, "Create"));
        async Task<bool> Allowed(Type controller, string method)
        {
            var data = controller.GetCustomAttributes(true).OfType<IAuthorizeData>()
                .Concat(controller.GetMethod(method)!.GetCustomAttributes(true).OfType<IAuthorizeData>());
            var policy = await AuthorizationPolicy.CombineAsync(provider.GetRequiredService<IAuthorizationPolicyProvider>(), data);
            return (await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(principal, null, policy!)).Succeeded;
        }
    }

    [Fact]
    public async Task Balance_permission_denial_returns_403_for_existing_safe_Web_feedback()
    {
        var proxy = DispatchProxy.Create<IStockBalanceService, DeniedBalanceProxy>();
        var controller = new StockBalancesController(proxy) {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())], "test")) } } };
        var result = await controller.GetAll(null, null, default);
        Assert.Equal(403, Assert.IsType<ObjectResult>(result.Result).StatusCode);
    }

    public class DeniedBalanceProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            Task.FromException<IReadOnlyCollection<CSM.Application.Inventory.StockBalances.Dtos.StockBalanceResponse>>(
                new StockBalanceManagementException("You are not authorized to access stock balances."));
    }

    private static StockItem Stock(Guid company, string name)
    {
        var site = new ConstructionSite { Id = Guid.NewGuid(), CompanyId = company, Name = name, SiteCode = name, IsActive = true };
        var material = new Material { Id = Guid.NewGuid(), CompanyId = company, Name = "Cement",
            MaterialCode = "MAT-001", UnitOfMeasure = "Bag", Category = "Cement" };
        return new StockItem { Id = Guid.NewGuid(), CompanyId = company, ConstructionSiteId = site.Id,
            ConstructionSite = site, MaterialId = material.Id, Material = material, QuantityOnHand = 6 };
    }
    private static TestDatabase Database() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private sealed class TestDatabase(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
    {
        public override Task<int> SaveChangesAsync(CancellationToken token = default) => base.SaveChangesAsync(true, token);
    }
    private sealed class Hasher : IPasswordHasher
    {
        public string Hash(string password) => "test-hash";
        public bool Verify(string password, string hash) => false;
    }
}
