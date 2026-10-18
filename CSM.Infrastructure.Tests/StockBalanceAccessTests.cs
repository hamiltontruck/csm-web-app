using System.Reflection;
using Bunit;
using CSM.Application.Common.Exceptions;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Entities.Procurement;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Services;
using CSM.Web.Components.Pages;
using CSM.Web.Models.Sites;
using CSM.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class StockBalanceAccessTests
{
    [Theory]
    [InlineData(AppRoles.SuperAdmin)]
    [InlineData(AppRoles.Administrator)]
    public async Task Companyless_admin_can_read_received_stock_in_list_and_detail(string role)
    {
        using var db = Database(role);
        var service = new StockBalanceService(db);
        var stock = Assert.Single(await service.GetAllAsync(ActorId, SiteId));
        Assert.Equal(10m, stock.QuantityOnHand);
        Assert.Equal("Cement", stock.MaterialName);
        Assert.Equal(10m, (await service.GetByIdAsync(ActorId, stock.StockItemId)).AvailableQuantity);
    }

    [Fact]
    public async Task Storekeeper_can_only_read_own_company_stock()
    {
        using var db = Database(AppRoles.Storekeeper);
        var service = new StockBalanceService(db);
        Assert.Empty(await service.GetAllAsync(ActorId));
        await Assert.ThrowsAsync<StockBalanceManagementException>(() =>
            service.GetByIdAsync(ActorId, db.StockItems.Single().Id));
    }

    [Fact]
    public async Task Unrelated_role_cannot_read_stock()
    {
        using var db = Database(AppRoles.Employee);
        await Assert.ThrowsAsync<StockBalanceManagementException>(() =>
            new StockBalanceService(db).GetAllAsync(ActorId));
    }

    [Fact]
    public void Site_picker_recovers_from_API_failure_without_killing_the_page()
    {
        using var context = Context();
        var proxy = (InventoryProxy)(object)context.Services.GetRequiredService<IInventoryService>();
        proxy.Fail = true;
        var page = context.RenderComponent<StockBalance>();
        page.WaitForAssertion(() => Assert.Contains("Unable to load stock balances", page.Find("[role=alert]").TextContent));
        Assert.DoesNotContain("Loading sites...", page.Markup);
        proxy.Fail = false;
        page.Find("#retry-stock-balances").Click();
        page.WaitForAssertion(() => Assert.Contains("1 material(s)", page.Markup));
        Assert.Contains("Hamilton Test Site", page.Markup);
        Assert.Empty(page.FindAll("[role=alert]"));
    }

    [Fact]
    public void Site_stock_page_recovers_from_API_failure_and_shows_received_quantity()
    {
        using var context = Context();
        var proxy = (InventoryProxy)(object)context.Services.GetRequiredService<IInventoryService>();
        proxy.Fail = true;
        var page = context.RenderComponent<StockBalanceSite>(p => p.Add(x => x.SiteId, SiteId));
        page.WaitForAssertion(() => Assert.Single(page.FindAll("[role=alert]")));
        Assert.DoesNotContain("Loading stock balance...", page.Markup);
        proxy.Fail = false;
        page.Find("#retry-stock-balances").Click();
        page.WaitForAssertion(() => Assert.Contains("Cement", page.Markup));
        Assert.Contains("10", page.Markup);
        Assert.Contains("Hamilton Test Site", page.Markup);
    }

    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly Guid SiteId = Guid.NewGuid();

    private static ApplicationDbContext Database(string role)
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var companyId = Guid.NewGuid();
        db.Users.Add(new User { Id = ActorId, IsActive = true,
            CompanyId = role == AppRoles.Storekeeper ? Guid.NewGuid() : null,
            UserRoles = [new UserRole { Id = Guid.NewGuid(), Role = new Role { Id = Guid.NewGuid(), Name = role } }] });
        db.StockItems.Add(new StockItem { Id = Guid.NewGuid(), CompanyId = companyId,
            ConstructionSiteId = SiteId, ConstructionSite = new ConstructionSite { Id = SiteId, CompanyId = companyId, Name = "Hamilton Test Site" },
            Material = new Material { Id = Guid.NewGuid(), CompanyId = companyId, MaterialCode = "MAT-001", Name = "Cement", Category = "Cement", UnitOfMeasure = "Bag" },
            QuantityOnHand = 10m, QuantityReserved = 0m });
        db.SaveChanges(acceptAllChangesOnSuccess: true);
        return db;
    }

    private static TestContext Context()
    {
        var context = new TestContext();
        context.Services.AddSingleton(DispatchProxy.Create<IProjectService, ProjectProxy>());
        context.Services.AddSingleton(DispatchProxy.Create<IInventoryService, InventoryProxy>());
        return context;
    }

    public class ProjectProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            Task.FromResult(new List<SiteResponse> { new() { Id = SiteId, Name = "Hamilton Test Site" } });
    }

    public class InventoryProxy : DispatchProxy
    {
        public bool Fail { get; set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            Fail ? Task.FromException<List<CSM.Web.Models.Procurement.StockBalanceResponse>>(new HttpRequestException("400 Bad Request")) :
            Task.FromResult(new List<CSM.Web.Models.Procurement.StockBalanceResponse> {
                new() { ConstructionSiteId = SiteId, MaterialCode = "MAT-001", MaterialName = "Cement", QuantityOnHand = 10m } });
    }
}
