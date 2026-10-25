using System.Reflection;
using System.Net;
using System.Security.Claims;
using Bunit;
using CSM.Api.Controllers;
using CSM.Application.Common.Exceptions;
using CSM.Application.Inventory.StockAdjustments;
using CSM.Application.Inventory.StockAdjustments.Dtos;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Entities.Procurement;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Services;
using CSM.Web.Components.Pages;
using CSM.Web.Models.Sites;
using CSM.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class StockAdjustmentAccessTests
{
    [Theory]
    [InlineData(AppRoles.SuperAdmin)]
    [InlineData(AppRoles.Administrator)]
    public async Task Admin_can_correct_six_to_eight_with_audited_movement(string role)
    {
        using var db = Database(role);
        var service = new StockAdjustmentService(db);
        var result = await service.CreateAsync(ActorId, Request());
        Assert.Equal(8m, db.StockItems.Single().QuantityOnHand);
        Assert.Equal(6m, result.QuantityBefore);
        Assert.Equal(8m, result.QuantityAfter);
        var record = Assert.Single(await service.GetAllAsync(ActorId));
        Assert.Equal("ADJ-001", record.AdjustmentNumber);
        Assert.Contains("Duplicate daily usage deduction", record.Remarks);
        Assert.Equal(6m, record.QuantityBefore);
        Assert.Equal(8m, record.QuantityAfter);
        Assert.Equal("StockAdjustment", db.StockMovements.Single().ReferenceType);
    }

    [Theory]
    [InlineData(AppRoles.Storekeeper)]
    [InlineData(AppRoles.Employee)]
    public async Task Unauthorized_user_cannot_adjust_other_company_stock(string role)
    {
        using var db = Database(role);
        await Assert.ThrowsAsync<StockAdjustmentManagementException>(() =>
            new StockAdjustmentService(db).CreateAsync(ActorId, Request()));
        Assert.Equal(6m, db.StockItems.Single().QuantityOnHand);
        Assert.Empty(db.StockMovements);
    }

    [Fact]
    public async Task Duplicate_adjustment_does_not_add_stock_twice()
    {
        using var db = Database(AppRoles.SuperAdmin);
        var service = new StockAdjustmentService(db);
        await service.CreateAsync(ActorId, Request());
        await Assert.ThrowsAsync<StockAdjustmentManagementException>(() => service.CreateAsync(ActorId, Request()));
        Assert.Equal(8m, db.StockItems.Single().QuantityOnHand);
        Assert.Single(db.StockMovements);
    }

    [Fact]
    public async Task Decrease_cannot_exceed_available_stock()
    {
        using var db = Database(AppRoles.SuperAdmin);
        var request = Request();
        request.Increase = false;
        request.Quantity = 7m;
        var error = await Assert.ThrowsAsync<StockAdjustmentManagementException>(() =>
            new StockAdjustmentService(db).CreateAsync(ActorId, request));
        Assert.Contains("Insufficient", error.Message);
        Assert.Equal(6m, db.StockItems.Single().QuantityOnHand);
    }

    [Fact]
    public void Site_page_can_retry_loading_without_crashing()
    {
        using var context = new TestContext();
        context.Services.AddSingleton(DispatchProxy.Create<IInventoryService, InventoryProxy>());
        context.Services.AddSingleton(DispatchProxy.Create<IProjectService, ProjectProxy>());
        var proxy = (InventoryProxy)(object)context.Services.GetRequiredService<IInventoryService>();
        proxy.Fail = true;
        var page = context.RenderComponent<StockAdjustmentsSite>(p => p.Add(x => x.SiteId, SiteId));
        page.WaitForAssertion(() => Assert.Single(page.FindAll("[role=alert]")));
        Assert.DoesNotContain("Loading adjustments...", page.Markup);
        proxy.Fail = false;
        page.Find("#retry-stock-adjustments").Click();
        page.WaitForAssertion(() => Assert.Contains("Cement", page.Markup));
        Assert.Contains("Hamilton Test Site", page.Markup);
        Assert.Empty(page.FindAll("[role=alert]"));
    }

    [Fact]
    public async Task Controller_returns_validation_response_for_service_rejection()
    {
        var service = DispatchProxy.Create<IStockAdjustmentService, RejectingService>();
        var controller = new StockAdjustmentsController(service) {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ActorId.ToString())], "test"))
            }}
        };
        var response = await controller.GetAll(default);
        Assert.IsType<BadRequestObjectResult>(response.Result);
    }


    [Fact]
    public async Task Supervisor_receives_403_for_adjustment_read_and_write_without_stock_changes()
    {
        using var db = Database(AppRoles.Supervisor);
        db.Users.Single().CompanyId = db.StockItems.Single().CompanyId;
        db.SaveChanges(acceptAllChangesOnSuccess: true);
        var controller = new StockAdjustmentsController(new StockAdjustmentService(db)) {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ActorId.ToString())], "test"))
            }}
        };
        Assert.Equal(403, Assert.IsType<ObjectResult>((await controller.GetAll(default)).Result).StatusCode);
        Assert.Equal(403, Assert.IsType<ObjectResult>((await controller.Create(Request(), default)).Result).StatusCode);
        Assert.Equal(6m, db.StockItems.Single().QuantityOnHand);
        Assert.Empty(db.StockMovements);
    }

    [Theory]
    [InlineData(401, "Your session is missing or has expired. Please sign in again.")]
    [InlineData(403, "You do not have permission to view stock adjustments.")]
    public void Adjustment_history_shows_auth_feedback_without_retry_or_creation(int status, string expected)
    {
        using var context = new TestContext();
        var inventory = DispatchProxy.Create<IInventoryService, InventoryProxy>();
        ((InventoryProxy)(object)inventory).Status = (HttpStatusCode)status;
        context.Services.AddSingleton(inventory);
        context.Services.AddSingleton(DispatchProxy.Create<IProjectService, ProjectProxy>());
        context.Services.AddSingleton(DispatchProxy.Create<IAuthService, AdjustmentAuth>());
        var page = context.RenderComponent<StockAdjustmentsSite>(p => p.Add(x => x.SiteId, SiteId));
        page.WaitForAssertion(() => Assert.Equal(expected, page.Find("[role=alert]").TextContent.Trim()));
        Assert.Empty(page.FindAll("#retry-stock-adjustments"));
        Assert.DoesNotContain("New Adjustment", page.Markup);
        Assert.DoesNotContain("No stock adjustments recorded", page.Markup);
        Assert.DoesNotContain("private API diagnostic", page.Markup);
        Assert.Contains("Back", page.Markup);
        Assert.Equal(status == 401, page.FindAll("#stock-adjustments-sign-in").Count == 1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Read_only_user_cannot_open_either_adjustment_create_route(bool siteRoute)
    {
        using var context = new TestContext();
        var auth = DispatchProxy.Create<IAuthService, AdjustmentAuth>();
        ((AdjustmentAuth)(object)auth).CanAdjust = false;
        context.Services.AddSingleton(auth);
        context.Services.AddSingleton(DispatchProxy.Create<IProjectService, ProjectProxy>());
        context.Services.AddSingleton(DispatchProxy.Create<IInventoryService, InventoryProxy>());
        var page = context.RenderComponent<StockAdjustmentCreate>(p => p.Add(x => x.SiteId, siteRoute ? SiteId : (Guid?)null));
        page.WaitForAssertion(() => Assert.Contains("You do not have permission to create stock adjustments.", page.Find("[role=alert]").TextContent));
        Assert.Empty(page.FindAll("input, select, textarea"));
        Assert.DoesNotContain("Save Adjustment", page.Markup);
        Assert.Contains("Back", page.Markup);
    }

    public class AdjustmentAuth : DispatchProxy
    {
        public bool CanAdjust { get; set; } = true;
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            Task.FromResult<IReadOnlyCollection<string>>(CanAdjust
                ? new[] { "Procurement_Inventory_View", "Procurement_Inventory_Adjust" }
                : new[] { "Procurement_Inventory_View" });
    }

    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly Guid SiteId = Guid.NewGuid();
    private static readonly Guid MaterialId = Guid.NewGuid();
    private static CreateStockAdjustmentRequest Request() => new() {
        ConstructionSiteId = SiteId, MaterialId = MaterialId, Increase = true, Quantity = 2m,
        AdjustmentDateUtc = DateTime.UtcNow, AdjustmentNumber = "ADJ-001",
        Reason = "Count Correction", Remarks = "Duplicate daily usage deduction"
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
            QuantityOnHand = 6m, QuantityReserved = 0m, AverageUnitCost = 1000m });
        db.SaveChanges(acceptAllChangesOnSuccess: true);
        return db;
    }

    private sealed class TestDatabase(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
    {
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            base.SaveChangesAsync(acceptAllChangesOnSuccess: true, cancellationToken);
    }

    public class RejectingService : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            Task.FromException<List<StockAdjustmentResponse>>(new StockAdjustmentManagementException("Not permitted."));
    }

    public class ProjectProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            Task.FromResult(new List<SiteResponse> { new() { Id = SiteId, Name = "Hamilton Test Site" } });
    }

    public class InventoryProxy : DispatchProxy
    {
        public bool Fail { get; set; }
        public HttpStatusCode? Status { get; set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            Status.HasValue ? Task.FromException<List<CSM.Web.Models.Procurement.StockAdjustmentResponse>>(new HttpRequestException("private API diagnostic", null, Status)) :
            Fail ? Task.FromException<List<CSM.Web.Models.Procurement.StockAdjustmentResponse>>(new HttpRequestException("500")) :
            Task.FromResult(new List<CSM.Web.Models.Procurement.StockAdjustmentResponse> {
                new() { ConstructionSiteId = SiteId, MaterialName = "Cement", Quantity = 2m, QuantityBefore = 6m, QuantityAfter = 8m } });
    }
}
