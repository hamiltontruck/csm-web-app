using System.Reflection;
using System.Net;
using System.Security.Claims;
using CSM.Api.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Bunit;
using CSM.Application.Common.Exceptions;
using CSM.Application.Inventory.StockIssues.Dtos;
using CSM.Application.Notifications;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Entities.Procurement;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Services;
using CSM.Web.Components.Pages;
using CSM.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class StockIssueAccessTests
{
    [Theory]
    [InlineData(AppRoles.SuperAdmin)]
    [InlineData(AppRoles.Administrator)]
    public async Task Companyless_admin_can_issue_stock_and_read_the_audit(string role)
    {
        using var db = Database(role);
        var service = new StockIssueService(db, new Notifications());
        var result = await service.CreateAsync(ActorId, Request());
        Assert.Equal(2m, result.QuantityIssued);
        Assert.Equal(8m, db.StockItems.Single().QuantityOnHand);
        Assert.Equal(10m, result.QuantityOnHandBefore);
        Assert.Equal(8m, result.QuantityOnHandAfter);
        Assert.Single(await service.GetAllAsync(ActorId));
        Assert.Equal("ISS-001", (await service.GetByIdAsync(ActorId, result.StockMovementId)).ReferenceNumber);
    }

    [Theory]
    [InlineData(AppRoles.Storekeeper)]
    [InlineData(AppRoles.Employee)]
    public async Task Unauthorized_actor_cannot_issue_other_company_stock(string role)
    {
        using var db = Database(role);
        await Assert.ThrowsAsync<StockIssueManagementException>(() =>
            new StockIssueService(db, new Notifications()).CreateAsync(ActorId, Request()));
        Assert.Equal(10m, db.StockItems.Single().QuantityOnHand);
        Assert.Empty(db.StockMovements);
    }

    [Fact]
    public async Task Admin_cannot_issue_more_than_available_stock()
    {
        using var db = Database(AppRoles.SuperAdmin);
        var request = Request();
        request.Quantity = 11m;
        var error = await Assert.ThrowsAsync<StockIssueManagementException>(() =>
            new StockIssueService(db, new Notifications()).CreateAsync(ActorId, request));
        Assert.Contains("exceeds", error.Message);
        Assert.Equal(10m, db.StockItems.Single().QuantityOnHand);
    }

    [Fact]
    public void Site_issues_page_recovers_from_API_failure_and_filters_the_site()
    {
        using var context = new TestContext();
        context.Services.AddSingleton(DispatchProxy.Create<IInventoryService, InventoryProxy>());
        var proxy = (InventoryProxy)(object)context.Services.GetRequiredService<IInventoryService>();
        proxy.Fail = true;
        var page = context.RenderComponent<StockIssuesSite>(p => p.Add(x => x.SiteId, SiteId));
        page.WaitForAssertion(() => Assert.Single(page.FindAll("[role=alert]")));
        Assert.DoesNotContain("Loading...", page.Markup);
        proxy.Fail = false;
        page.Find("#retry-stock-issues").Click();
        page.WaitForAssertion(() => Assert.Contains("Cement", page.Markup));
        Assert.Contains("ISS-001", page.Markup);
        Assert.DoesNotContain("Other site material", page.Markup);
    }

    [Theory]
    [InlineData(AppRoles.Storekeeper)]
    [InlineData(AppRoles.ProcurementOfficer)]
    public async Task Inventory_role_can_manage_own_company_but_cannot_read_or_write_another_company(string role)
    {
        using var db = Database(role);
        var actor = db.Users.Single();
        actor.CompanyId = db.StockItems.Single().CompanyId;
        db.SaveChanges(acceptAllChangesOnSuccess: true);
        var service = new StockIssueService(db, new Notifications());
        var result = await service.CreateAsync(ActorId, Request());
        Assert.Single(await service.GetAllAsync(ActorId));
        Assert.NotNull(await service.GetByIdAsync(ActorId, result.StockMovementId));
        var balance = db.StockItems.Single().QuantityOnHand;
        var movements = db.StockMovements.Count();

        actor.CompanyId = Guid.NewGuid();
        db.SaveChanges(acceptAllChangesOnSuccess: true);
        Assert.Empty(await service.GetAllAsync(ActorId));
        await Assert.ThrowsAsync<StockIssueManagementException>(() => service.GetByIdAsync(ActorId, result.StockMovementId));
        var request = Request();
        request.ReferenceNumber = "OTHER-COMPANY-TEST";
        await Assert.ThrowsAsync<StockIssueManagementException>(() => service.CreateAsync(ActorId, request));
        Assert.Equal(balance, db.StockItems.Single().QuantityOnHand);
        Assert.Equal(movements, db.StockMovements.Count());
    }

    [Theory]
    [InlineData(AppRoles.Storekeeper)]
    [InlineData(AppRoles.ProcurementOfficer)]
    public async Task Inventory_role_without_company_cannot_write_stock(string role)
    {
        using var db = Database(role);
        db.Users.Single().CompanyId = null;
        db.SaveChanges(acceptAllChangesOnSuccess: true);
        var service = new StockIssueService(db, new Notifications());
        var balance = db.StockItems.Single().QuantityOnHand;
        var movements = db.StockMovements.Count();
        await Assert.ThrowsAsync<StockIssueManagementException>(() => service.CreateAsync(ActorId, Request()));
        Assert.Equal(balance, db.StockItems.Single().QuantityOnHand);
        Assert.Equal(movements, db.StockMovements.Count());
    }

    [Fact]
    public async Task Company_membership_alone_does_not_grant_inventory_management()
    {
        using var db = Database(AppRoles.Employee);
        db.Users.Single().CompanyId = db.StockItems.Single().CompanyId;
        db.SaveChanges(acceptAllChangesOnSuccess: true);
        var service = new StockIssueService(db, new Notifications());
        var balance = db.StockItems.Single().QuantityOnHand;
        await Assert.ThrowsAsync<StockIssueManagementException>(() => service.CreateAsync(ActorId, Request()));
        await Assert.ThrowsAsync<StockIssueManagementException>(() => service.GetAllAsync(ActorId));
        Assert.Equal(balance, db.StockItems.Single().QuantityOnHand);
    }


    [Fact]
    public async Task Supervisor_denial_returns_403_and_cannot_change_own_company_stock()
    {
        using var db = Database(AppRoles.Supervisor);
        db.Users.Single().CompanyId = db.StockItems.Single().CompanyId;
        db.SaveChanges(acceptAllChangesOnSuccess: true);
        var controller = new StockIssuesController(new StockIssueService(db, new Notifications())) {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ActorId.ToString())], "test")) } } };
        var list = await controller.GetAll(default);
        var detail = await controller.GetById(Guid.NewGuid(), default);
        var create = await controller.Create(Request(), default);
        Assert.Equal(403, Assert.IsType<ObjectResult>(list.Result).StatusCode);
        Assert.Equal(403, Assert.IsType<ObjectResult>(detail.Result).StatusCode);
        Assert.Equal(403, Assert.IsType<ObjectResult>(create.Result).StatusCode);
        Assert.Equal(10m, db.StockItems.Single().QuantityOnHand);
        Assert.Empty(db.StockMovements);
    }

    [Theory]
    [InlineData(401, "Your session is missing or has expired. Please sign in again.")]
    [InlineData(403, "You do not have permission to view stock issues.")]
    public void Site_issues_page_shows_auth_feedback_without_retry_or_issue_controls(int status, string expected)
    {
        using var context = new TestContext();
        var inventory = DispatchProxy.Create<IInventoryService, InventoryProxy>();
        ((InventoryProxy)(object)inventory).Status = (HttpStatusCode)status;
        context.Services.AddSingleton(inventory);
        var page = context.RenderComponent<StockIssuesSite>(p => p.Add(x => x.SiteId, SiteId));
        page.WaitForAssertion(() => Assert.Equal(expected, Assert.Single(page.FindAll("[role=alert]")).TextContent.Trim()));
        Assert.Empty(page.FindAll("#retry-stock-issues"));
        Assert.DoesNotContain("New Issue", page.Markup);
        Assert.DoesNotContain("No stock issues for this site.", page.Markup);
        Assert.DoesNotContain("private API error", page.Markup);
        Assert.Equal(status == 401, page.FindAll("#stock-issues-sign-in").Count == 1);
    }

    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly Guid SiteId = Guid.NewGuid();
    private static readonly Guid MaterialId = Guid.NewGuid();

    private static CreateStockIssueRequest Request() => new() {
        ConstructionSiteId = SiteId, MaterialId = MaterialId, Quantity = 2m,
        IssueDateUtc = DateTime.UtcNow, ReferenceNumber = "ISS-001"
    };

    private static TestDatabase Database(string role)
    {
        var db = new TestDatabase(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
        var companyId = Guid.NewGuid();
        db.Users.Add(new User { Id = ActorId, IsActive = true,
            CompanyId = role == AppRoles.Storekeeper ? Guid.NewGuid() : null,
            UserRoles = [new UserRole { Id = Guid.NewGuid(), Role = new Role { Id = Guid.NewGuid(), Name = role } }] });
        db.StockItems.Add(new StockItem { Id = Guid.NewGuid(), CompanyId = companyId, MaterialId = MaterialId,
            ConstructionSiteId = SiteId, ConstructionSite = new ConstructionSite { Id = SiteId, CompanyId = companyId, Name = "Hamilton Test Site" },
            Material = new Material { Id = MaterialId, CompanyId = companyId, MaterialCode = "MAT-001", Name = "Cement", Category = "Cement", UnitOfMeasure = "Bag" },
            QuantityOnHand = 10m, QuantityReserved = 0m, AverageUnitCost = 1000m });
        db.SaveChanges(acceptAllChangesOnSuccess: true);
        return db;
    }

    private sealed class TestDatabase(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
    {
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            base.SaveChangesAsync(acceptAllChangesOnSuccess: true, cancellationToken);
    }

    private sealed class Notifications : ILowStockNotificationService
    {
        public Task NotifyIfLowStockAsync(Guid stockItemId, Guid actorUserId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    public class InventoryProxy : DispatchProxy
    {
        public bool Fail { get; set; }
        public HttpStatusCode? Status { get; set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            Status.HasValue ? Task.FromException<List<CSM.Web.Models.Procurement.StockIssueResponse>>(new HttpRequestException("private API error", null, Status)) :
            Fail ? Task.FromException<List<CSM.Web.Models.Procurement.StockIssueResponse>>(new HttpRequestException("400 Bad Request")) :
            Task.FromResult(new List<CSM.Web.Models.Procurement.StockIssueResponse> {
                new() { ConstructionSiteId = SiteId, MaterialName = "Cement", ReferenceNumber = "ISS-001", QuantityIssued = 2m },
                new() { ConstructionSiteId = Guid.NewGuid(), MaterialName = "Other site material" } });
    }
}
