using System.Text.Json;
using CSM.Application.Common.Exceptions;
using CSM.Application.Finance.Invoices.Dtos;
using CSM.Application.Finance.Payments.Dtos;
using CSM.Domain.Entities.Finance;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Security;
using CSM.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace CSM.Infrastructure.Tests;
public sealed class FinanceCompanyContextTests
{
    [Fact] public async Task Companyless_superadmin_reads_site_expenses()
    {
        using var f = new Fixture();
        Assert.Equal(f.Company.Id, Assert.Single(await new ExpenseService(f.Db).GetBySiteAsync(f.Actor.Id, f.Site.Id)).CompanyId);
        Assert.Null(f.Actor.CompanyId);
    }
    [Fact] public async Task Companyless_superadmin_reads_site_budgets()
    {
        using var f = new Fixture();
        Assert.Equal(f.Company.Id, Assert.Single(await new BudgetService(f.Db, new Permissions()).GetBySiteAsync(f.Actor.Id, f.Site.Id)).CompanyId);
    }
    [Fact] public async Task Companyless_superadmin_creates_site_invoice()
    {
        using var f = new Fixture();
        Assert.Equal(f.Company.Id, (await new InvoiceService(f.Db).CreateAsync(f.Actor.Id, f.InvoiceRequest(f.Site.Id))).CompanyId);
    }
    [Fact] public async Task Companyless_superadmin_creates_selected_company_invoice()
    {
        using var f = new Fixture();
        var request = WithCompany(f.InvoiceRequest(null), f.Company.Id);
        Assert.Equal(f.Company.Id, (await new InvoiceService(f.Db).CreateAsync(f.Actor.Id, request)).CompanyId);
    }
    [Fact] public async Task Companyless_superadmin_creates_invoice_payment()
    {
        using var f = new Fixture();
        Assert.Equal(f.Company.Id, (await new PaymentService(f.Db).CreateAsync(f.Actor.Id, f.PaymentRequest())).CompanyId);
        Assert.Null(f.Actor.CompanyId);
    }
    [Fact] public async Task Ordinary_actor_cannot_create_foreign_site_invoice()
    {
        using var f = new Fixture("Accountant", Guid.NewGuid());
        await Assert.ThrowsAsync<InvoiceManagementException>(() => new InvoiceService(f.Db).CreateAsync(f.Actor.Id, f.InvoiceRequest(f.Site.Id)));
        Assert.Single(f.Db.Invoices);
    }
    [Fact] public async Task Site_vendor_options_are_scoped_to_selected_company()
    {
        using var f = new Fixture();
        f.Db.Vendors.Add(new Vendor { CompanyId = Guid.NewGuid(), VendorCode = "OTHER", Name = "Other" }); f.Db.SaveChanges(true);
        var options = await new InvoiceService(f.Db).GetVendorOptionsAsync(f.Actor.Id, siteId: f.Site.Id);
        Assert.Equal(f.Vendor.Id, Assert.Single(options).Id);
    }
    [Fact] public async Task Global_options_expose_only_active_companies_and_do_not_assign_actor()
    {
        using var f = new Fixture();
        f.Db.Companies.Add(new Company { Code = "OFF", Name = "Inactive", IsActive = false }); f.Db.SaveChanges(true);
        var invoice = await new InvoiceService(f.Db).GetCompanyOptionsAsync(f.Actor.Id, true);
        var payment = await new PaymentService(f.Db).GetCompanyOptionsAsync(f.Actor.Id, true);
        Assert.True(invoice.CanSelectCompany); Assert.Equal(f.Company.Id, Assert.Single(invoice.Companies).Id);
        Assert.Equal(f.Company.Id, Assert.Single(payment.Companies).Id); Assert.Null(f.Actor.CompanyId);
    }
    [Fact] public async Task Company_lists_accept_explicit_selection_and_preserve_omitted_own_company()
    {
        using var f = new Fixture();
        f.Invoice.ConstructionSiteId = null;
        f.Db.Payments.Add(new Payment { CompanyId = f.Company.Id, InvoiceId = f.Invoice.Id, PaymentNumber = "COMPANY-PAY" });
        var own = new Company { Code = "OWN", Name = "Assigned", IsActive = true }; f.Db.Companies.Add(own);
        f.Actor.CompanyId = own.Id; f.Db.SaveChanges(true);
        var invoices = new InvoiceService(f.Db); var payments = new PaymentService(f.Db);
        Assert.Empty(await invoices.GetCompanyInvoicesAsync(f.Actor.Id));
        Assert.Empty(await payments.GetCompanyPaymentsAsync(f.Actor.Id));
        Assert.Equal(f.Invoice.Id, Assert.Single(await invoices.GetCompanyInvoicesAsync(f.Actor.Id, companyId: f.Company.Id)).Id);
        Assert.Equal(f.Company.Id, Assert.Single(await payments.GetCompanyPaymentsAsync(f.Actor.Id, companyId: f.Company.Id)).CompanyId);
    }
    [Fact] public async Task Selected_company_must_match_site_for_invoice_and_payment()
    {
        using var f = new Fixture();
        await Assert.ThrowsAsync<InvoiceManagementException>(() => new InvoiceService(f.Db).CreateAsync(f.Actor.Id, WithCompany(f.InvoiceRequest(f.Site.Id), Guid.NewGuid())));
        await Assert.ThrowsAsync<PaymentManagementException>(() => new PaymentService(f.Db).CreateAsync(f.Actor.Id, WithCompany(f.PaymentRequest(), Guid.NewGuid())));
        Assert.Empty(f.Db.Payments); Assert.Single(f.Db.Invoices);
    }
    [Fact] public async Task Companyless_superadmin_creates_approved_payroll_payment()
    {
        using var f = new Fixture();
        var payroll = new CSM.Domain.Entities.HRM.PayrollRecord { CompanyId = f.Company.Id, Status = PayrollStatus.Approved, NetPay = 100 };
        f.Db.PayrollRecords.Add(payroll); f.Db.SaveChanges(true);
        var request = f.PaymentRequest() with { ConstructionSiteId = null, InvoiceId = null, VendorId = null, PayrollRecordId = payroll.Id };
        Assert.Equal(f.Company.Id, (await new PaymentService(f.Db).CreateAsync(f.Actor.Id, request)).CompanyId);
    }
    [Fact] public async Task Companyless_superadmin_creates_vendor_only_payment_without_company_selection()
    {
        using var f = new Fixture();
        var request = f.PaymentRequest() with { ConstructionSiteId = null, InvoiceId = null };
        var payment = await new PaymentService(f.Db).CreateAsync(f.Actor.Id, request);
        Assert.Equal(f.Company.Id, payment.CompanyId);
        Assert.Equal(f.Vendor.Id, payment.VendorId);
        Assert.Null(f.Actor.CompanyId);
    }
    [Fact] public async Task Deleted_vendor_cannot_identify_payment_company()
    {
        using var f = new Fixture();
        f.Vendor.IsDeleted = true; f.Db.SaveChanges(true);
        var request = f.PaymentRequest() with { ConstructionSiteId = null, InvoiceId = null };
        await Assert.ThrowsAsync<PaymentManagementException>(() => new PaymentService(f.Db).CreateAsync(f.Actor.Id, request));
        Assert.Empty(f.Db.Payments);
    }
    [Fact] public async Task Payment_project_and_vendor_must_share_company()
    {
        using var f = new Fixture();
        var project = new CSM.Domain.Entities.Projects.Project { CompanyId = Guid.NewGuid(), ConstructionSiteId = f.Site.Id, ProjectCode = "FOREIGN", Name = "Foreign" };
        f.Db.Projects.Add(project); f.Db.SaveChanges(true);
        var request = f.PaymentRequest() with { ConstructionSiteId = null, InvoiceId = null, ProjectId = project.Id };
        await Assert.ThrowsAsync<PaymentManagementException>(() => new PaymentService(f.Db).CreateAsync(f.Actor.Id, request));
        Assert.Empty(f.Db.Payments);
    }
    [Fact] public async Task Companyless_superadmin_creates_payment_from_matching_project_and_vendor()
    {
        using var f = new Fixture();
        var project = new CSM.Domain.Entities.Projects.Project { CompanyId = f.Company.Id, ConstructionSiteId = f.Site.Id, ProjectCode = "OWN", Name = "Own" };
        f.Db.Projects.Add(project); f.Db.SaveChanges(true);
        var request = f.PaymentRequest() with { ConstructionSiteId = null, InvoiceId = null, ProjectId = project.Id };
        Assert.Equal(f.Company.Id, (await new PaymentService(f.Db).CreateAsync(f.Actor.Id, request)).CompanyId);
    }
    [Fact] public async Task Cross_company_payment_reference_is_rejected()
    {
        using var f = new Fixture();
        f.Invoice.CompanyId = Guid.NewGuid(); f.Db.SaveChanges(true);
        await Assert.ThrowsAsync<PaymentManagementException>(() => new PaymentService(f.Db).CreateAsync(f.Actor.Id, f.PaymentRequest()));
        Assert.Empty(f.Db.Payments);
    }
    [Fact] public async Task Ordinary_actor_cannot_select_foreign_company_for_company_reads_or_options()
    {
        using var f = new Fixture("Accountant", Guid.NewGuid());
        await Assert.ThrowsAsync<InvoiceManagementException>(() => new InvoiceService(f.Db).GetCompanyInvoicesAsync(f.Actor.Id, companyId: f.Company.Id));
        await Assert.ThrowsAsync<PaymentManagementException>(() => new PaymentService(f.Db).GetCompanyPaymentsAsync(f.Actor.Id, companyId: f.Company.Id));
        Assert.False((await new InvoiceService(f.Db).GetCompanyOptionsAsync(f.Actor.Id)).CanSelectCompany);
    }
    [Fact] public async Task Missing_company_selection_and_deleted_site_are_rejected()
    {
        using var f = new Fixture();
        await Assert.ThrowsAsync<InvoiceManagementException>(() => new InvoiceService(f.Db).GetCompanyInvoicesAsync(f.Actor.Id));
        await Assert.ThrowsAsync<InvoiceManagementException>(() => new InvoiceService(f.Db).CreateAsync(f.Actor.Id, f.InvoiceRequest(null)));
        f.Site.IsDeleted = true; f.Db.SaveChanges(true);
        await Assert.ThrowsAsync<ExpenseManagementException>(() => new ExpenseService(f.Db).GetBySiteAsync(f.Actor.Id, f.Site.Id));
        await Assert.ThrowsAsync<BudgetManagementException>(() => new BudgetService(f.Db, new Permissions()).GetBySiteAsync(f.Actor.Id, f.Site.Id));
    }
    [Fact] public async Task Ungranted_superadmin_cannot_derive_global_site_company()
    {
        using var f = new Fixture();
        f.Db.RoleGranularPermissions.RemoveRange(f.Db.RoleGranularPermissions); f.Db.SaveChanges(true);
        await Assert.ThrowsAsync<ExpenseManagementException>(() => new ExpenseService(f.Db).GetBySiteAsync(f.Actor.Id, f.Site.Id));
        await Assert.ThrowsAsync<BudgetManagementException>(() => new BudgetService(f.Db, new Permissions()).GetBySiteAsync(f.Actor.Id, f.Site.Id));
    }
    private static T WithCompany<T>(T request, Guid company) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(request).TrimEnd('}') + $",\"CompanyId\":\"{company}\"}}")!;
    private sealed class Fixture : IDisposable
    {
        public TestDatabase Db { get; } = new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        public Company Company { get; } = new() { Name = "Selected", Code = "SEL", IsActive = true };
        public ConstructionSite Site { get; }
        public User Actor { get; }
        public Invoice Invoice { get; }
        public Vendor Vendor { get; }
        public Fixture(string role = "SuperAdmin", Guid? actorCompany = null)
        {
            Actor = new User { CompanyId = actorCompany, IsActive = true, UserRoles = [new UserRole { Role = new Role { Name = role, GranularPermissions = [new() { Permission = GranularPermission.Finance_Invoice_View }, new() { Permission = GranularPermission.Finance_Payment_Manage }, new() { Permission = GranularPermission.Finance_Payment_View }, new() { Permission = GranularPermission.Finance_Expense_View }, new() { Permission = GranularPermission.Finance_Budget_View }] } }] };
            Site = new ConstructionSite { CompanyId = Company.Id, Name = "Selected site", SiteCode = "SITE", IsActive = true };
            Vendor = new Vendor { CompanyId = Company.Id, Name = "Selected vendor", VendorCode = "VEN" };
            Invoice = new Invoice { CompanyId = Company.Id, ConstructionSiteId = Site.Id, VendorId = Vendor.Id, InvoiceNumber = "EXISTING", Type = InvoiceType.VendorBill, Status = InvoiceStatus.Issued, TotalAmount = 100, CurrencyCode = "ETB", ExchangeRate = 1 };
            Db.AddRange(Company, Site, Actor, Vendor, Invoice, new Expense { CompanyId = Company.Id, ConstructionSiteId = Site.Id, ExpenseNumber = "EXP" }, new Budget { CompanyId = Company.Id, ConstructionSiteId = Site.Id, BudgetNumber = "BUD", Name = "Budget", EffectiveFrom = new DateOnly(2026, 1, 1) });
            Db.SaveChanges(true);
        }
        public CreateInvoiceRequest InvoiceRequest(Guid? site) => new(site, null, Vendor.Id, "NEW", InvoiceType.VendorBill, new DateOnly(2026, 1, 1), null, 100, 0, 100, "ETB", 1, null, null);
        public CreatePaymentRequest PaymentRequest() => new(Site.Id, null, Invoice.Id, Vendor.Id, null, "PAY-NEW", new DateOnly(2026, 1, 1), PaymentDirection.Outgoing, 20, "ETB", 1, "Cash", null, null);
        public void Dispose() => Db.Dispose();
    }
    private sealed class TestDatabase(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
    {
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => base.SaveChangesAsync(true, cancellationToken);
    }
    private sealed class Permissions : IPermissionEvaluator
    {
        public Task<bool> HasPermissionAsync(Guid id, string permission, CancellationToken token = default) => Task.FromResult(true);
        public Task<IReadOnlyCollection<string>> GetPermissionsAsync(Guid id, CancellationToken token = default) => Task.FromResult<IReadOnlyCollection<string>>([]);
    }
}
