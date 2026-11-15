using System.Text.Json;
using CSM.Application.Finance.Invoices.Dtos;
using CSM.Application.Finance.Payments.Dtos;
using CSM.Application.HRM.Shifts.Dtos;
using CSM.Domain.Entities.Finance;
using CSM.Domain.Entities.HRM;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Enums;
using CSM.Infrastructure.Security;
using CSM.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class SuperAdminCompanyPostgresTests
{
    [Theory]
    [InlineData("leave")]
    [InlineData("shift")]
    [InlineData("invoice")]
    [InlineData("payment")]
    [InlineData("site-expenses")]
    [InlineData("site-budgets")]
    public async Task Authorized_companyless_admin_uses_action_context_with_real_foreign_keys(string action)
    {
        await using var fixture = await FinanceSettlementPostgresFixture.CreateAsync();
        await using var db = fixture.Context();
        var company = new Company { Code = "CTX", Name = "Context company", IsActive = true };
        var other = new Company { Code = "OTHER", Name = "Other company", IsActive = true };
        var role = FinanceOfficerAuthorityTests.Role("SuperAdmin",
            GranularPermission.HRM_Leave_View, GranularPermission.HRM_Leave_Submit,
            GranularPermission.HRM_Shift_View, GranularPermission.HRM_Shift_Submit,
            GranularPermission.Finance_Invoice_View, GranularPermission.Finance_Payment_View,
            GranularPermission.Finance_Payment_Manage, GranularPermission.Finance_Expense_View,
            GranularPermission.Finance_Budget_View);
        var actor = new User { Email = "context@example.test", NormalizedEmail = "CONTEXT@EXAMPLE.TEST", IsActive = true,
            UserRoles = [new() { Role = role }] };
        var site = new ConstructionSite { CompanyId = company.Id, SiteCode = "SITE", Name = "Context site", IsActive = true };
        var employee = new Employee { CompanyId = company.Id, EmployeeNumber = "EMP", FirstName = "Context", LastName = "Employee",
            HireDate = new(2026, 1, 1), Status = EmployeeStatus.Active };
        var vendor = new Vendor { CompanyId = company.Id, VendorCode = "VENDOR", Name = "Context vendor" };
        var invoice = new Invoice { CompanyId = company.Id, ConstructionSiteId = site.Id, VendorId = vendor.Id,
            InvoiceNumber = "EXISTING", Type = InvoiceType.VendorBill, Status = InvoiceStatus.Issued,
            InvoiceDate = new(2026, 1, 1), Subtotal = 100, TotalAmount = 100, CurrencyCode = "ETB", ExchangeRate = 1 };
        db.AddRange(company, other, actor, site, employee, vendor, invoice);
        await db.SaveChangesAsync();
        var day = new DateOnly(2026, 11, 1);
        Guid? savedCompany = null;
        switch (action)
        {
            case "leave":
                savedCompany = (await new LeaveService(db).CreateAsync(actor.Id, new(employee.Id, "Annual", day, day, null))).CompanyId;
                break;
            case "shift":
                var request = JsonSerializer.Deserialize<CreateShiftRequest>(JsonSerializer.Serialize(new {
                    CompanyId = company.Id, Code = "DAY", Name = "Day", StartTime = new TimeOnly(8, 0),
                    EndTime = new TimeOnly(16, 0), StandardHours = 8m }))!;
                savedCompany = (await new ShiftService(db).CreateAsync(actor.Id, request)).CompanyId;
                break;
            case "invoice":
                savedCompany = (await new InvoiceService(db).CreateAsync(actor.Id, new CreateInvoiceRequest(site.Id, null,
                    vendor.Id, "NEW", InvoiceType.VendorBill, day, null, 100, 0, 100, "ETB", 1, null, null))).CompanyId;
                break;
            case "payment":
                savedCompany = (await new PaymentService(db).CreateAsync(actor.Id, new CreatePaymentRequest(site.Id, null,
                    invoice.Id, vendor.Id, null, "PAY-NEW", day, PaymentDirection.Outgoing, 20, "ETB", 1, "Cash", null, null))).CompanyId;
                break;
            case "site-expenses":
                Assert.Empty(await new ExpenseService(db).GetBySiteAsync(actor.Id, site.Id));
                break;
            case "site-budgets":
                Assert.Empty(await new BudgetService(db, new PermissionEvaluator(db)).GetBySiteAsync(actor.Id, site.Id));
                break;
        }
        if (savedCompany.HasValue) Assert.Equal(company.Id, savedCompany.Value);
        await using var check = fixture.Context();
        Assert.Null((await check.Users.SingleAsync(x => x.Id == actor.Id)).CompanyId);
        if (action == "leave") Assert.Equal(company.Id, (await check.LeaveRequests.SingleAsync()).CompanyId);
        if (action == "shift") Assert.Equal(company.Id, (await check.Shifts.SingleAsync()).CompanyId);
        if (action == "invoice") Assert.Equal(company.Id, (await check.Invoices.SingleAsync(x => x.InvoiceNumber == "NEW")).CompanyId);
        if (action == "payment") Assert.Equal(company.Id, (await check.Payments.SingleAsync()).CompanyId);
        Assert.DoesNotContain(await check.Payments.ToListAsync(), x => x.CompanyId == other.Id);
        Assert.DoesNotContain(await check.Invoices.ToListAsync(), x => x.CompanyId == other.Id);
    }
}
