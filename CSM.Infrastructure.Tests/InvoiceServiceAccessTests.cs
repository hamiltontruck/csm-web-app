using CSM.Application.Common.Exceptions;
using CSM.Application.Finance.Invoices.Dtos;
using CSM.Application.Finance.InvoiceLines.Dtos;
using CSM.Domain.Entities.Finance;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Entities.Projects;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class InvoiceServiceAccessTests
{
    public static IEnumerable<object[]> DeniedCalls =>
        from condition in new[] { "inactive", "deleted", "revoked", "deleted-role", "deleted-link", "empty-company" }
        from method in new[] { "company", "site", "invoice", "create", "update", "status", "issue", "cancel", "lines", "line", "line-create", "line-update", "line-delete" }
        select new object[] { condition, method };

    [Theory, MemberData(nameof(DeniedCalls))]
    public async Task Every_entry_point_requires_active_actor_and_current_invoice_grant(string condition, string method)
    {
        using var f = new Fixture();
        if (condition == "inactive") f.Actor.IsActive = false;
        if (condition == "empty-company") f.Actor.CompanyId = Guid.Empty;
        if (condition == "deleted") f.Actor.IsDeleted = true;
        if (condition == "revoked") f.Db.RoleGranularPermissions.RemoveRange(f.Db.RoleGranularPermissions);
        if (condition == "deleted-role") f.Actor.UserRoles.Single().Role.IsDeleted = true;
        if (condition == "deleted-link") f.Actor.UserRoles.Single().IsDeleted = true;
        f.Save();
        var error = await Record.ExceptionAsync(() => f.Call(method));
        Assert.NotNull(error);
        if (method.StartsWith("line")) Assert.IsType<InvoiceLineManagementException>(error);
        else Assert.IsType<InvoiceManagementException>(error);
        Assert.Equal(InvoiceStatus.Draft, f.Invoice.Status);
        Assert.Equal(1m, f.Line.Quantity);
        Assert.False(f.Line.IsDeleted);
    }

    [Theory]
    [InlineData("Supervisor")]
    [InlineData("SiteManager")]
    public async Task Company_list_retains_existing_finance_role_ceiling(string role)
    {
        using var f = new Fixture(role);
        await Assert.ThrowsAsync<InvoiceManagementException>(() => new InvoiceService(f.Db).GetCompanyInvoicesAsync(f.Actor.Id));
    }

    [Theory]
    [InlineData("get")]
    [InlineData("update")]
    [InlineData("delete")]
    public async Task Line_cannot_access_or_mutate_a_foreign_invoice_through_malformed_company_link(string operation)
    {
        using var f = new Fixture();
        f.Line.InvoiceId = f.ForeignInvoice.Id; f.Save();
        var service = new InvoiceLineService(f.Db);
        await Assert.ThrowsAsync<InvoiceLineManagementException>(async () =>
        {
            if (operation == "get") await service.GetByIdAsync(f.Actor.Id, f.Line.Id);
            if (operation == "update") await service.UpdateAsync(f.Actor.Id, f.Line.Id, LineUpdate);
            if (operation == "delete") await service.DeleteAsync(f.Actor.Id, f.Line.Id);
        });
        Assert.Equal(1m, f.Line.Quantity);
        Assert.False(f.Line.IsDeleted);
        Assert.Equal(0m, f.ForeignInvoice.TotalAmount);
    }

    [Fact]
    public async Task Invoice_line_list_excludes_foreign_company_children()
    {
        using var f = new Fixture();
        f.Line.CompanyId = f.ForeignInvoice.CompanyId; f.Save();
        Assert.Empty(await new InvoiceLineService(f.Db).GetByInvoiceAsync(f.Actor.Id, f.Invoice.Id));
    }

    [Theory]
    [InlineData("CompanyAdmin")]
    [InlineData("Accountant")]
    [InlineData("SuperAdmin")]
    public async Task Authorized_roles_retain_invoice_and_line_lifecycle(string role)
    {
        using var f = new Fixture(role);
        var invoices = new InvoiceService(f.Db); var lines = new InvoiceLineService(f.Db);
        var row = await invoices.CreateAsync(f.Actor.Id, CreateRequest);
        Assert.Contains(await invoices.GetCompanyInvoicesAsync(f.Actor.Id), x => x.Id == row.Id);
        var line = await lines.CreateAsync(f.Actor.Id, row.Id, LineCreate);
        Assert.Equal(7m, (await invoices.GetByIdAsync(f.Actor.Id, row.Id)).TotalAmount);
        await lines.UpdateAsync(f.Actor.Id, line.Id, LineUpdate);
        Assert.Equal(11m, (await invoices.GetByIdAsync(f.Actor.Id, row.Id)).TotalAmount);
        await lines.DeleteAsync(f.Actor.Id, line.Id);
        Assert.Empty(await lines.GetByInvoiceAsync(f.Actor.Id, row.Id));
        Assert.Equal(0m, (await invoices.GetByIdAsync(f.Actor.Id, row.Id)).TotalAmount);
        await invoices.UpdateAsync(f.Actor.Id, row.Id, UpdateRequest);
        Assert.Equal(InvoiceStatus.Issued, (await invoices.IssueAsync(f.Actor.Id, row.Id)).Status);
        Assert.Equal(InvoiceStatus.Cancelled, (await invoices.CancelAsync(f.Actor.Id, row.Id)).Status);
    }

    [Fact]
    public async Task Company_reader_cannot_read_or_change_foreign_records()
    {
        using var f = new Fixture(); var invoices = new InvoiceService(f.Db); var lines = new InvoiceLineService(f.Db);
        await Assert.ThrowsAsync<InvoiceManagementException>(() => invoices.GetByIdAsync(f.Actor.Id, f.ForeignInvoice.Id));
        await Assert.ThrowsAsync<InvoiceManagementException>(() => invoices.IssueAsync(f.Actor.Id, f.ForeignInvoice.Id));
        await Assert.ThrowsAsync<InvoiceLineManagementException>(() => lines.CreateAsync(f.Actor.Id, f.ForeignInvoice.Id, LineCreate));
        Assert.DoesNotContain(await invoices.GetCompanyInvoicesAsync(f.Actor.Id), x => x.Id == f.ForeignInvoice.Id);
    }

    [Fact]
    public async Task Super_admin_retains_global_detail_access_but_company_list_stays_own_company()
    {
        using var f = new Fixture("SuperAdmin"); var service = new InvoiceService(f.Db);
        Assert.Equal(f.ForeignInvoice.Id, (await service.GetByIdAsync(f.Actor.Id, f.ForeignInvoice.Id)).Id);
        Assert.DoesNotContain(await service.GetCompanyInvoicesAsync(f.Actor.Id), x => x.Id == f.ForeignInvoice.Id);
    }

    [Fact]
    public async Task Invoice_details_and_paid_totals_exclude_foreign_company_payments()
    {
        using var f = new Fixture();
        f.Db.Payments.AddRange(
            new Payment { CompanyId = f.Invoice.CompanyId, InvoiceId = f.Invoice.Id, PaymentNumber = "OWN", Status = PaymentStatus.Completed, Amount = 2m },
            new Payment { CompanyId = f.ForeignInvoice.CompanyId, InvoiceId = f.Invoice.Id, PaymentNumber = "FOREIGN", Status = PaymentStatus.Completed, Amount = 100m });
        f.Save();
        var detail = await new InvoiceService(f.Db).GetByIdAsync(f.Actor.Id, f.Invoice.Id);
        Assert.Equal(2m, detail.PaidAmount);
        Assert.Equal("OWN", Assert.Single(detail.Payments).PaymentNumber);
    }

    [Fact]
    public async Task Malformed_foreign_lines_do_not_enter_own_invoice_totals()
    {
        using var f = new Fixture();
        f.Line.CompanyId = f.ForeignInvoice.CompanyId; f.Save();
        await new InvoiceLineService(f.Db).CreateAsync(f.Actor.Id, f.Invoice.Id, LineCreate);
        Assert.Equal(7m, f.Invoice.TotalAmount);
    }

    [Theory]
    [InlineData("site")]
    [InlineData("project")]
    [InlineData("vendor")]
    public async Task Invoice_create_and_update_reject_foreign_parent_references(string parent)
    {
        using var f = new Fixture(); var foreign = f.ForeignInvoice.CompanyId;
        var site = new ConstructionSite { CompanyId = foreign };
        var project = new Project { CompanyId = foreign, ConstructionSiteId = site.Id };
        var vendor = new Vendor { CompanyId = foreign };
        f.Db.ConstructionSites.Add(site); f.Db.Projects.Add(project); f.Db.Vendors.Add(vendor); f.Save();
        var request = CreateRequest with { ConstructionSiteId = parent == "site" ? site.Id : null, ProjectId = parent == "project" ? project.Id : null, VendorId = parent == "vendor" ? vendor.Id : null, Type = parent == "vendor" ? InvoiceType.VendorBill : InvoiceType.ClientInvoice };
        var service = new InvoiceService(f.Db);
        await Assert.ThrowsAsync<InvoiceManagementException>(() => service.CreateAsync(f.Actor.Id, request));
        await Assert.ThrowsAsync<InvoiceManagementException>(() => service.UpdateAsync(f.Actor.Id, f.Invoice.Id, UpdateRequest with { ConstructionSiteId = request.ConstructionSiteId, ProjectId = request.ProjectId, VendorId = request.VendorId, Type = request.Type }));
        Assert.Equal(2, f.Db.Invoices.Count());
        Assert.Null(f.Invoice.ProjectId); Assert.Null(f.Invoice.VendorId); Assert.Null(f.Invoice.ConstructionSiteId);
    }

    [Fact]
    public async Task Invoice_lines_reject_foreign_cost_code_before_any_write()
    {
        using var f = new Fixture();
        var code = new CostCode { CompanyId = f.ForeignInvoice.CompanyId };
        f.Db.CostCodes.Add(code); f.Save();
        var service = new InvoiceLineService(f.Db);
        await Assert.ThrowsAsync<InvoiceLineManagementException>(() => service.CreateAsync(f.Actor.Id, f.Invoice.Id, LineCreate with { CostCodeId = code.Id }));
        await Assert.ThrowsAsync<InvoiceLineManagementException>(() => service.UpdateAsync(f.Actor.Id, f.Line.Id, LineUpdate with { CostCodeId = code.Id }));
        Assert.Single(f.Db.InvoiceLines); Assert.Null(f.Line.CostCodeId); Assert.Equal(1m, f.Line.Quantity);
    }

    [Fact]
    public async Task PostgreSQL_enforces_live_grants_parent_scopes_and_preserves_line_calculation()
    {
        var connection = new Npgsql.NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("CSM_TEST_POSTGRES") ?? "Host=localhost;Username=postgres;Password=postgres") { Database = "invoice_access_" + Guid.NewGuid().ToString("N") };
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection.ConnectionString).Options);
        try
        {
            await db.Database.MigrateAsync();
            var company = new Company { Code = "OWN", Name = "Own" };
            var foreign = new Company { Code = "OTHER", Name = "Other" };
            var actor = new User { CompanyId = company.Id, Email = "invoice@example.test", NormalizedEmail = "INVOICE@EXAMPLE.TEST", IsActive = true, UserRoles = [new() { Role = new Role { Name = "Accountant", NormalizedName = "ACCOUNTANT", GranularPermissions = [new() { Permission = GranularPermission.Finance_Invoice_View }] } }] };
            db.Companies.AddRange(company, foreign); db.Users.Add(actor); await db.SaveChangesAsync();
            var invoices = new InvoiceService(db); var lines = new InvoiceLineService(db);
            var invoice = await invoices.CreateAsync(actor.Id, CreateRequest);
            var foreignInvoice = new Invoice { CompanyId = foreign.Id, InvoiceNumber = "FOREIGN", InvoiceDate = new DateOnly(2026, 1, 1) };
            db.Invoices.Add(foreignInvoice); await db.SaveChangesAsync();
            var line = await lines.CreateAsync(actor.Id, invoice.Id, LineCreate);
            Assert.Equal(7m, (await invoices.GetByIdAsync(actor.Id, invoice.Id)).TotalAmount);
            var trackedLine = await db.InvoiceLines.SingleAsync(x => x.Id == line.Id);
            trackedLine.InvoiceId = foreignInvoice.Id; await db.SaveChangesAsync();
            await Assert.ThrowsAsync<InvoiceLineManagementException>(() => lines.GetByIdAsync(actor.Id, line.Id));
            await Assert.ThrowsAsync<InvoiceLineManagementException>(() => lines.UpdateAsync(actor.Id, line.Id, LineUpdate));
            Assert.Empty(await lines.GetByInvoiceAsync(actor.Id, invoice.Id));
            db.RoleGranularPermissions.RemoveRange(db.RoleGranularPermissions.Where(x => x.RoleId == actor.UserRoles.Single().RoleId)); await db.SaveChangesAsync();
            await Assert.ThrowsAsync<InvoiceManagementException>(() => invoices.GetByIdAsync(actor.Id, invoice.Id));
            await Assert.ThrowsAsync<InvoiceLineManagementException>(() => lines.GetByInvoiceAsync(actor.Id, invoice.Id));
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private static CreateInvoiceRequest CreateRequest => new(null, null, null, "NEW", InvoiceType.ClientInvoice, new DateOnly(2026, 1, 1), null, 0m, 0m, 0m, "USD", 1m, null, null);
    private static UpdateInvoiceRequest UpdateRequest => new(null, null, null, InvoiceType.ClientInvoice, new DateOnly(2026, 1, 1), null, 0m, 0m, 0m, "USD", 1m, null, null);
    private static CreateInvoiceLineRequest LineCreate => new(null, "line", 2m, "Pcs", 3m, 1m);
    private static UpdateInvoiceLineRequest LineUpdate => new(null, "changed", 2m, "Pcs", 5m, 1m);

    private sealed class Fixture : IDisposable
    {
        public TestDb Db = new();
        public User Actor;
        public Invoice Invoice, ForeignInvoice;
        public InvoiceLine Line;
        public Fixture(string role = "Accountant")
        {
            var company = Guid.NewGuid();
            Actor = new User { CompanyId = company, IsActive = true, UserRoles = [new() { Role = new Role { Name = role, GranularPermissions = [new() { Permission = GranularPermission.Finance_Invoice_View }] } }] };
            Invoice = new Invoice { CompanyId = company, InvoiceNumber = "OWN", Status = InvoiceStatus.Draft };
            ForeignInvoice = new Invoice { CompanyId = Guid.NewGuid(), InvoiceNumber = "FOREIGN", Status = InvoiceStatus.Draft };
            Line = new InvoiceLine { CompanyId = company, InvoiceId = Invoice.Id, Description = "original", Quantity = 1m, UnitPrice = 1m, LineTotal = 1m };
            Db.Users.Add(Actor); Db.Invoices.AddRange(Invoice, ForeignInvoice); Db.InvoiceLines.Add(Line); Save();
        }
        public void Save() => Db.SaveChanges(true);
        public async Task Call(string method)
        {
            var invoices = new InvoiceService(Db); var lines = new InvoiceLineService(Db);
            switch (method)
            {
                case "company": await invoices.GetCompanyInvoicesAsync(Actor.Id); break;
                case "site": await invoices.GetAllAsync(Actor.Id, Guid.NewGuid()); break;
                case "invoice": await invoices.GetByIdAsync(Actor.Id, Invoice.Id); break;
                case "create": await invoices.CreateAsync(Actor.Id, CreateRequest); break;
                case "update": await invoices.UpdateAsync(Actor.Id, Invoice.Id, UpdateRequest); break;
                case "status": await invoices.ChangeStatusAsync(Actor.Id, Invoice.Id, new(InvoiceStatus.Issued)); break;
                case "issue": await invoices.IssueAsync(Actor.Id, Invoice.Id); break;
                case "cancel": await invoices.CancelAsync(Actor.Id, Invoice.Id); break;
                case "lines": await lines.GetByInvoiceAsync(Actor.Id, Invoice.Id); break;
                case "line": await lines.GetByIdAsync(Actor.Id, Line.Id); break;
                case "line-create": await lines.CreateAsync(Actor.Id, Invoice.Id, LineCreate); break;
                case "line-update": await lines.UpdateAsync(Actor.Id, Line.Id, LineUpdate); break;
                case "line-delete": await lines.DeleteAsync(Actor.Id, Line.Id); break;
                default: throw new ArgumentException(method);
            }
        }
        public void Dispose() => Db.Dispose();
    }
    public sealed class TestDb : ApplicationDbContext
    {
        public TestDb() : base(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options) { }
        public override Task<int> SaveChangesAsync(CancellationToken ct = default) => base.SaveChangesAsync(true, ct);
    }
}
