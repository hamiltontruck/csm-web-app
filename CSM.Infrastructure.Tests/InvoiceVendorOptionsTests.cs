using System.Reflection;
using System.Text.Json;
using Bunit;
using CSM.Domain.Entities.Finance;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Services;
using CSM.Web.Models.Finance;
using CSM.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Page = CSM.Web.Components.Pages.Finance.Company.CreateInvoice;

namespace CSM.Infrastructure.Tests;

public sealed class InvoiceVendorOptionsTests
{
    private static readonly Guid CompanyId = Guid.NewGuid(), VendorId = Guid.NewGuid(), InvoiceId = Guid.NewGuid();

    [Fact]
    public void Company_create_loads_invoice_vendor_options_without_catalog_access_and_saves_computed_total()
    {
        using var context = Context(out var service);
        var page = context.RenderComponent<Page>();
        page.WaitForAssertion(() => Assert.Contains("Hamilton Vendor", page.Markup));
        page.Find("input").Change("ACC-INV-TEST-001");
        page.Find("select").Change(VendorId.ToString());
        page.FindAll("input[type=number]").ElementAt(1).Change("1000");
        page.FindAll("input[type=number]").ElementAt(2).Change("150");
        var total = Assert.Single(page.FindAll("input[type=number][readonly]"));
        Assert.Equal(1150m, decimal.Parse(total.GetAttribute("value")!));
        Assert.Equal(4, page.FindAll("input[type=number]").Count);
        page.Find("textarea").Change("Accountant invoice test.");
        page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.Equal($"http://localhost/finance/invoices/{InvoiceId}", context.Services.GetRequiredService<NavigationManager>().Uri));
        Assert.NotNull(service.Saved);
        Assert.Equal(VendorId, service.Saved.VendorId);
        Assert.Equal("ACC-INV-TEST-001", service.Saved.InvoiceNumber);
        Assert.Equal(1000m, service.Saved.Subtotal);
        Assert.Equal(150m, service.Saved.TaxAmount);
        Assert.Equal(1150m, service.Saved.TotalAmount);
        Assert.Null(service.Saved.ConstructionSiteId);
        Assert.Null(service.Saved.ProjectId);
        Assert.Equal("ETB", service.Saved.CurrencyCode);
    }

    [Fact]
    public void Failed_vendor_load_is_local_and_disables_create_instead_of_terminating_the_circuit()
    {
        using var context = Context(out var service);
        service.LoadFails = true;
        var page = context.RenderComponent<Page>();
        page.WaitForAssertion(() => Assert.Contains("Vendor lookup unavailable", page.Find("[role=alert]").TextContent));
        Assert.True(page.Find("button[type=submit]").HasAttribute("disabled"));
        page.FindAll("button").Single(x => x.TextContent.Trim() == "Cancel").Click();
        Assert.Equal("http://localhost/finance/company/invoices", context.Services.GetRequiredService<NavigationManager>().Uri);
        Assert.Null(service.Saved);
    }

    [Fact]
    public void Failed_save_preserves_form_and_allows_retry()
    {
        using var context = Context(out var service);
        service.SaveFails = true;
        var page = context.RenderComponent<Page>();
        page.WaitForAssertion(() => Assert.Contains("Hamilton Vendor", page.Markup));
        page.Find("input").Change("ACC-INV-TEST-001");
        page.Find("select").Change(VendorId.ToString());
        page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.Contains("Invoice rejected", page.Find("[role=alert]").TextContent));
        Assert.Equal("ACC-INV-TEST-001", page.Find("input").GetAttribute("value"));
        Assert.False(page.Find("button[type=submit]").HasAttribute("disabled"));
        Assert.Equal("http://localhost/", context.Services.GetRequiredService<NavigationManager>().Uri);
    }

    [Fact]
    public async Task Vendor_lookup_requires_live_invoice_grant()
    {
        Assert.NotNull(typeof(CSM.Api.Controllers.InvoicesController).GetMethod("GetVendorOptions"));
        await new BackendActionPermissionTests().HTTP_action_requires_its_own_permission_even_when_all_role_checks_pass(
            "InvoicesController", "GetVendorOptions", GranularPermission.Finance_Invoice_View, GranularPermission.Finance_Invoice_View);
    }

    [Theory]
    [InlineData("Accountant")]
    [InlineData("CompanyAdmin")]
    [InlineData("SuperAdmin")]
    public async Task Invoice_vendor_options_are_own_company_only_and_exclude_private_vendor_fields(string role)
    {
        using var db = Database(role);
        var options = await Options(db);
        var row = Assert.Single(options.EnumerateArray());
        Assert.Equal(VendorId, row.GetProperty("Id").GetGuid());
        Assert.Equal("Hamilton Vendor", row.GetProperty("Name").GetString());
        Assert.Equal(new[] { "Id", "Name", "VendorCode" }, row.EnumerateObject().Select(p => p.Name).Order().ToArray());
    }

    [Theory]
    [InlineData("inactive")]
    [InlineData("deleted")]
    [InlineData("revoked")]
    [InlineData("companyless")]
    [InlineData("ineligible")]
    public async Task Vendor_lookup_rejects_invalid_actor_or_revoked_grant(string condition)
    {
        using var db = Database(condition == "ineligible" ? "SiteManager" : "Accountant");
        var actor = db.Users.Single();
        if (condition == "inactive") actor.IsActive = false;
        if (condition == "deleted") actor.IsDeleted = true;
        if (condition == "companyless") actor.CompanyId = null;
        if (condition == "revoked") db.RoleGranularPermissions.RemoveRange(db.RoleGranularPermissions);
        db.SaveChanges(true);
        await Assert.ThrowsAsync<CSM.Application.Common.Exceptions.InvoiceManagementException>(() => Options(db));
    }

    private static async Task<JsonElement> Options(ApplicationDbContext db)
    {
        var method = typeof(InvoiceService).GetMethod("GetVendorOptionsAsync");
        Assert.NotNull(method);
        var task = (Task)method.Invoke(new InvoiceService(db), new object[] { db.Users.Single().Id, CancellationToken.None })!;
        await task;
        return JsonSerializer.SerializeToElement(task.GetType().GetProperty("Result")!.GetValue(task));
    }

    private static ApplicationDbContext Database(string role)
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Users.Add(new User { CompanyId = CompanyId, IsActive = true, UserRoles = [new UserRole { Role = new Role { Name = role, GranularPermissions = [new() { Permission = GranularPermission.Finance_Invoice_View }] } }] });
        db.Vendors.AddRange(new Vendor { Id = VendorId, CompanyId = CompanyId, VendorCode = "HBG-VEN-001", Name = "Hamilton Vendor", BankAccountNumber = "private" },
            new Vendor { CompanyId = Guid.NewGuid(), VendorCode = "OTHER", Name = "Other Company Vendor" },
            new Vendor { CompanyId = CompanyId, VendorCode = "DELETED", Name = "Deleted Vendor", IsDeleted = true });
        db.SaveChanges(true);
        return db;
    }

    private static TestContext Context(out FinanceProxy state)
    {
        var context = new TestContext();
        context.Services.AddLogging();
        var finance = DispatchProxy.Create<IFinanceService, FinanceProxy>();
        state = (FinanceProxy)(object)finance;
        context.Services.AddSingleton(finance);
        context.Services.AddSingleton(DispatchProxy.Create<IVendorService, DeniedVendorProxy>());
        return context;
    }

    public class DeniedVendorProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            Task.FromException<List<CSM.Web.Models.Procurement.VendorResponse>>(new HttpRequestException("403 Forbidden"));
    }

    public class FinanceProxy : DispatchProxy
    {
        public bool LoadFails, SaveFails;
        public CreateInvoiceRequest? Saved;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == "GetInvoiceVendorOptionsAsync")
            {
                var type = method.ReturnType.GenericTypeArguments.Single();
                if (LoadFails) return typeof(Task).GetMethods().Single(m => m.Name == nameof(Task.FromException) && m.IsGenericMethod)
                    .MakeGenericMethod(type).Invoke(null, new object[] { new HttpRequestException("Vendor lookup unavailable") });
                var list = JsonSerializer.Deserialize($"[{{\"Id\":\"{VendorId}\",\"VendorCode\":\"HBG-VEN-001\",\"Name\":\"Hamilton Vendor\"}}]", type);
                return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(type).Invoke(null, new[] { list });
            }
            if (method.Name == "CreateInvoiceAsync")
            {
                Saved = (CreateInvoiceRequest)args![0]!;
                return SaveFails ? Task.FromException<Guid>(new HttpRequestException("Invoice rejected")) : Task.FromResult(InvoiceId);
            }
            throw new NotSupportedException(method.Name);
        }
    }
}
