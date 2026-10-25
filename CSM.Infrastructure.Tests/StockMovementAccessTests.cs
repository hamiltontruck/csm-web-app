using System.Reflection;
using System.Net;
using System.Security.Claims;
using CSM.Api.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Bunit;
using CSM.Application.Common.Exceptions;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Entities.Procurement;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Services;
using CSM.Web.Components.Pages;
using CSM.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class StockMovementAccessTests
{
    [Theory]
    [InlineData(AppRoles.SuperAdmin)]
    [InlineData(AppRoles.Administrator)]
    public async Task Companyless_admin_can_read_complete_history_and_detail(string role)
    {
        using var db = Database(role);
        var service = new StockMovementService(db);
        var result = await service.GetAllAsync(ActorId, SiteId, MaterialId);
        Assert.Equal(4, result.Count);
        Assert.Contains(result, x => x.ReferenceNumber == "GR-001");
        Assert.Contains(result, x => x.ReferenceNumber == "ISS-001");
        Assert.Contains(result, x => x.ReferenceType == "DailyMaterialUsage");
        var correction = Assert.Single(result.Where(x => x.ReferenceNumber == "ADJ-CORR-001"));
        Assert.Equal(6m, correction.StockBalanceBefore);
        Assert.Equal(8m, correction.StockBalanceAfter);
        Assert.Equal(correction.Id, (await service.GetByIdAsync(ActorId, correction.Id)).Id);
        Assert.Equal(8m, db.StockItems.Single().QuantityOnHand);
    }

    [Fact]
    public async Task Storekeeper_cannot_read_another_company_history_or_detail()
    {
        using var db = Database(AppRoles.Storekeeper);
        var service = new StockMovementService(db);
        Assert.Empty(await service.GetAllAsync(ActorId));
        await Assert.ThrowsAsync<StockMovementManagementException>(() =>
            service.GetByIdAsync(ActorId, db.StockMovements.First().Id));
    }

    [Fact]
    public async Task Unrelated_role_cannot_read_movement_history()
    {
        using var db = Database(AppRoles.Employee);
        await Assert.ThrowsAsync<StockMovementManagementException>(() =>
            new StockMovementService(db).GetAllAsync(ActorId));
    }

    [Fact]
    public async Task Movement_filters_and_invalid_date_validation_remain_enforced()
    {
        using var db = Database(AppRoles.SuperAdmin);
        var service = new StockMovementService(db);
        Assert.Single(await service.GetAllAsync(ActorId, SiteId, MaterialId, StockMovementType.GoodsReceipt));
        Assert.Empty(await service.GetAllAsync(ActorId, Guid.NewGuid()));
        await Assert.ThrowsAsync<StockMovementManagementException>(() =>
            service.GetAllAsync(ActorId, fromUtc: DateTime.UtcNow, toUtc: DateTime.UtcNow.AddDays(-1)));
    }

    [Fact]
    public void Site_history_recovers_after_API_error_and_filters_other_sites()
    {
        using var context = new TestContext();
        context.Services.AddSingleton(DispatchProxy.Create<IInventoryService, InventoryProxy>());
        var proxy = (InventoryProxy)(object)context.Services.GetRequiredService<IInventoryService>();
        proxy.Fail = true;
        var page = context.RenderComponent<StockMovementsSite>(p => p.Add(x => x.SiteId, SiteId));
        page.WaitForAssertion(() => Assert.Single(page.FindAll("[role=alert]")));
        Assert.DoesNotContain("Loading stock movements...", page.Markup);
        proxy.Fail = false;
        page.Find("#retry-stock-movements").Click();
        page.WaitForAssertion(() => Assert.Contains("ADJ-CORR-001", page.Markup));
        Assert.Contains("Cement", page.Markup);
        Assert.DoesNotContain("Other site", page.Markup);
        Assert.Empty(page.FindAll("[role=alert]"));
    }


    [Fact]
    public async Task Supervisor_receives_403_for_history_and_detail_without_changing_stock()
    {
        using var db = Database(AppRoles.Supervisor);
        db.Users.Single().CompanyId = db.StockItems.Single().CompanyId;
        db.SaveChanges(acceptAllChangesOnSuccess: true);
        var controller = Controller(db);
        var list = await controller.GetAll(null, null, null, null, null, default);
        var detail = await controller.GetById(db.StockMovements.First().Id, default);
        Assert.Equal(403, Assert.IsType<ObjectResult>(list.Result).StatusCode);
        Assert.Equal(403, Assert.IsType<ObjectResult>(detail.Result).StatusCode);
        Assert.Equal(8m, db.StockItems.Single().QuantityOnHand);
        Assert.Equal(4, db.StockMovements.Count());
    }

    [Fact]
    public async Task Invalid_date_range_keeps_HTTP_400_validation_response()
    {
        using var db = Database(AppRoles.SuperAdmin);
        var response = await Controller(db).GetAll(null, null, null,
            DateTime.UtcNow, DateTime.UtcNow.AddDays(-1), default);
        Assert.IsType<BadRequestObjectResult>(response.Result);
    }

    [Theory]
    [InlineData(401, "Your session is missing or has expired. Please sign in again.")]
    [InlineData(403, "You do not have permission to view stock movements.")]
    public void History_distinguishes_auth_errors_without_retry_or_empty_state(int status, string expected)
    {
        using var context = new TestContext();
        var inventory = DispatchProxy.Create<IInventoryService, InventoryProxy>();
        ((InventoryProxy)(object)inventory).Status = (HttpStatusCode)status;
        context.Services.AddSingleton(inventory);
        var page = context.RenderComponent<StockMovementsSite>(p => p.Add(x => x.SiteId, SiteId));
        page.WaitForAssertion(() => Assert.Equal(expected, page.Find("[role=alert]").TextContent.Trim()));
        Assert.Empty(page.FindAll("#retry-stock-movements"));
        Assert.DoesNotContain("No stock movements found.", page.Markup);
        Assert.DoesNotContain("private API diagnostic", page.Markup);
        Assert.Contains("Back", page.Markup);
        Assert.Equal(status == 401, page.FindAll("#stock-movements-sign-in").Count == 1);
    }

    private static StockMovementsController Controller(ApplicationDbContext db) =>
        new(new StockMovementService(db)) {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ActorId.ToString())], "test"))
            }}
        };

    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly Guid SiteId = Guid.NewGuid();
    private static readonly Guid MaterialId = Guid.NewGuid();

    private static ApplicationDbContext Database(string role)
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var companyId = Guid.NewGuid();
        db.Users.Add(new User { Id = ActorId, IsActive = true,
            CompanyId = role == AppRoles.Storekeeper ? Guid.NewGuid() : null,
            UserRoles = [new UserRole { Id = Guid.NewGuid(), Role = new Role { Id = Guid.NewGuid(), Name = role } }] });
        var stock = new StockItem { Id = Guid.NewGuid(), CompanyId = companyId, MaterialId = MaterialId,
            ConstructionSiteId = SiteId, ConstructionSite = new ConstructionSite { Id = SiteId, CompanyId = companyId, Name = "Hamilton Test Site" },
            Material = new Material { Id = MaterialId, CompanyId = companyId, MaterialCode = "MAT-001", Name = "Cement", Category = "Cement", UnitOfMeasure = "Bag" },
            QuantityOnHand = 8m };
        db.StockItems.Add(stock);
        var rows = new[] {
            (StockMovementType.GoodsReceipt, "GoodsReceipt", "GR-001", 10m, 0m, 10m),
            (StockMovementType.Issue, "StockIssue", "ISS-001", 2m, 10m, 8m),
            (StockMovementType.Issue, "DailyMaterialUsage", "DMU-001", 2m, 8m, 6m),
            (StockMovementType.AdjustmentIncrease, "StockAdjustment", "ADJ-CORR-001", 2m, 6m, 8m)
        };
        foreach (var (type, referenceType, referenceNumber, quantity, before, after) in rows)
            db.StockMovements.Add(new StockMovement { Id = Guid.NewGuid(), CompanyId = companyId,
                StockItem = stock, StockItemId = stock.Id, MovementType = type,
                ReferenceType = referenceType, ReferenceNumber = referenceNumber, MovementDateUtc = DateTime.UtcNow,
                Quantity = quantity, StockBalanceBefore = before, StockBalanceAfter = after });
        db.SaveChanges(acceptAllChangesOnSuccess: true);
        return db;
    }

    public class InventoryProxy : DispatchProxy
    {
        public bool Fail { get; set; }
        public HttpStatusCode? Status { get; set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            Status.HasValue ? Task.FromException<List<CSM.Web.Models.Procurement.StockMovementResponse>>(new HttpRequestException("private API diagnostic", null, Status)) :
            Fail ? Task.FromException<List<CSM.Web.Models.Procurement.StockMovementResponse>>(new HttpRequestException("400")) :
            Task.FromResult(new List<CSM.Web.Models.Procurement.StockMovementResponse> {
                new() { ConstructionSiteId = SiteId, SiteName = "Hamilton Test Site", MaterialName = "Cement",
                    MovementType = "AdjustmentIncrease", ReferenceNumber = "ADJ-CORR-001", StockBalanceBefore = 6m, StockBalanceAfter = 8m },
                new() { ConstructionSiteId = Guid.NewGuid(), MaterialName = "Other site" } });
    }
}
