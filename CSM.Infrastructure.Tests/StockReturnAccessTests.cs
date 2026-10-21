using System.Reflection;
using Bunit;
using CSM.Application.Common.Exceptions;
using CSM.Application.Inventory.StockReturns.Dtos;
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

public sealed class StockReturnAccessTests
{
    [Theory]
    [InlineData(AppRoles.SuperAdmin)]
    [InlineData(AppRoles.Administrator)]
    public async Task Companyless_admin_can_return_stock_and_read_the_audit(string role)
    {
        using var db = Database(role);
        var service = new StockReturnService(db);
        var result = await service.CreateAsync(ActorId, Request());
        Assert.Equal(2m, result.ReturnedQuantity);
        Assert.Equal(12m, db.StockItems.Single().QuantityOnHand);
        Assert.Equal(10m, result.QuantityOnHandBefore);
        Assert.Equal(12m, result.QuantityOnHandAfter);
        Assert.Single(await service.GetAllAsync(ActorId));
        Assert.Equal("ISS-001", (await service.GetByIdAsync(ActorId, result.Id)).ReferenceNumber);
    }

    [Theory]
    [InlineData(AppRoles.Storekeeper)]
    [InlineData(AppRoles.Employee)]
    public async Task Unauthorized_actor_cannot_return_other_company_stock(string role)
    {
        using var db = Database(role);
        await Assert.ThrowsAsync<StockReturnManagementException>(() =>
            new StockReturnService(db).CreateAsync(ActorId, Request()));
        Assert.Equal(10m, db.StockItems.Single().QuantityOnHand);
        Assert.Single(db.StockMovements);
    }

    [Fact]
    public async Task Admin_cannot_return_more_than_original_issue()
    {
        using var db = Database(AppRoles.SuperAdmin);
        var request = Request();
        request.Quantity = 11m;
        var error = await Assert.ThrowsAsync<StockReturnManagementException>(() =>
            new StockReturnService(db).CreateAsync(ActorId, request));
        Assert.Contains("exceeds", error.Message);
        Assert.Equal(10m, db.StockItems.Single().QuantityOnHand);
    }

    [Fact]
    public void Site_returns_page_recovers_from_API_failure_and_filters_the_site()
    {
        using var context = new TestContext();
        context.Services.AddSingleton(DispatchProxy.Create<IInventoryService, InventoryProxy>());
        var proxy = (InventoryProxy)(object)context.Services.GetRequiredService<IInventoryService>();
        proxy.Fail = true;
        var page = context.RenderComponent<StockReturnsSite>(p => p.Add(x => x.SiteId, SiteId));
        page.WaitForAssertion(() => Assert.Single(page.FindAll("[role=alert]")));
        Assert.DoesNotContain("Loading...", page.Markup);
        proxy.Fail = false;
        page.Find("#retry-stock-returns").Click();
        page.WaitForAssertion(() => Assert.Contains("Cement", page.Markup));
        Assert.Contains("ISS-001", page.Markup);
        Assert.DoesNotContain("Other site material", page.Markup);
    }

    [Fact]
    public void Site_picker_recovers_from_API_failure()
    {
        using var context = new TestContext();
        var service = DispatchProxy.Create<IProjectService, ProjectProxy>();
        context.Services.AddSingleton(service);
        var page = context.RenderComponent<StockReturns>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("[role=alert]")));
        Assert.DoesNotContain("Loading construction sites", page.Markup);
        ((ProjectProxy)(object)service).Fail = false;
        page.Find("#retry-stock-return-sites").Click();
        page.WaitForAssertion(() => Assert.Contains("Hamilton Test Site", page.Markup));
    }

    public class ProjectProxy : DispatchProxy
    {
        public bool Fail { get; set; } = true;
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            Fail ? Task.FromException<List<CSM.Web.Models.Sites.SiteResponse>>(new HttpRequestException("503")) :
            Task.FromResult(new List<CSM.Web.Models.Sites.SiteResponse> { new() { Id = SiteId, Name = "Hamilton Test Site" } });
    }

    private static readonly Guid IssueId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly Guid SiteId = Guid.NewGuid();
    private static readonly Guid MaterialId = Guid.NewGuid();

    private static CreateStockReturnRequest Request() => new() {
        OriginalIssueMovementId = IssueId, Quantity = 2m,
        ReturnDateUtc = DateTime.UtcNow, ReferenceNumber = "ISS-001"
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
        db.StockMovements.Add(new StockMovement { Id = IssueId, CompanyId = companyId, StockItemId = db.StockItems.Local.Single().Id, MovementType = CSM.Domain.Enums.StockMovementType.Issue, Quantity = 10m, MovementDateUtc = DateTime.UtcNow });
        db.SaveChanges(acceptAllChangesOnSuccess: true);
        return db;
    }

    private sealed class TestDatabase(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
    {
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            base.SaveChangesAsync(acceptAllChangesOnSuccess: true, cancellationToken);
    }

    public class InventoryProxy : DispatchProxy
    {
        public bool Fail { get; set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            Fail ? Task.FromException<List<CSM.Web.Models.Procurement.StockReturnResponse>>(new HttpRequestException("400 Bad Request")) :
            Task.FromResult(new List<CSM.Web.Models.Procurement.StockReturnResponse> {
                new() { ConstructionSiteId = SiteId, MaterialName = "Cement", ReferenceNumber = "ISS-001", ReturnedQuantity = 2m },
                new() { ConstructionSiteId = Guid.NewGuid(), MaterialName = "Other site material" } });
    }
}
