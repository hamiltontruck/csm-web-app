using CSM.Domain.Entities.Finance;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Enums;
using CSM.Infrastructure.Security;
using CSM.Infrastructure.Services;
using Xunit;
namespace CSM.Infrastructure.Tests;
public sealed class FinanceOfficerServiceScopeTests
{
    [Theory]
    [InlineData("budget")] [InlineData("invoice")] [InlineData("expense")] [InlineData("payment")]
    [InlineData("petty-account")] [InlineData("petty-transactions")] [InlineData("budget-lines")] [InlineData("invoice-lines")]
    public async Task Assigned_finance_reader_can_read_its_site_and_cannot_guess_another_sites_parent(string kind)
    {
        using var db = FinanceOfficerAuthorityTests.Database(); var actor = FinanceOfficerAuthorityTests.Actor(); db.Users.Add(actor);
        var company = actor.CompanyId!.Value;
        var sites = new[] { new ConstructionSite { CompanyId = company, Name = "Assigned", SiteCode = "A", IsActive = true }, new ConstructionSite { CompanyId = company, Name = "Other", SiteCode = "B", IsActive = true } };
        db.ConstructionSites.AddRange(sites);
        db.UserSiteAccesses.Add(new() { CompanyId = company, UserId = actor.Id, SiteId = sites[0].Id, IsActive = true });
        var ids = new List<Guid>();
        foreach (var site in sites)
        {
            Guid id;
            if (kind is "budget" or "budget-lines") { var row = new Budget { CompanyId = company, ConstructionSiteId = site.Id, BudgetNumber = site.SiteCode, EffectiveFrom = new DateOnly(2026,11,11) }; db.Budgets.Add(row); id = row.Id; }
            else if (kind is "invoice" or "invoice-lines") { var row = new Invoice { CompanyId = company, ConstructionSiteId = site.Id, InvoiceNumber = site.SiteCode }; db.Invoices.Add(row); id = row.Id; }
            else if (kind == "expense") { var row = new Expense { CompanyId = company, ConstructionSiteId = site.Id, ExpenseNumber = site.SiteCode }; db.Expenses.Add(row); id = row.Id; }
            else if (kind == "payment") { var row = new Payment { CompanyId = company, ConstructionSiteId = site.Id, PaymentNumber = site.SiteCode }; db.Payments.Add(row); id = row.Id; }
            else { var row = new PettyCashAccount { CompanyId = company, ConstructionSiteId = site.Id, AccountName = site.Name }; db.PettyCashAccounts.Add(row); id = row.Id; }
            ids.Add(id);
        }
        await db.SaveChangesAsync();
        async Task Read(Guid id)
        {
            switch (kind)
            {
                case "budget": await new BudgetService(db, new PermissionEvaluator(db)).GetByIdAsync(actor.Id, id); break;
                case "invoice": await new InvoiceService(db).GetByIdAsync(actor.Id, id); break;
                case "expense": await new ExpenseService(db).GetByIdAsync(actor.Id, id); break;
                case "payment": await new PaymentService(db).GetByIdAsync(actor.Id, id); break;
                case "petty-account": await new PettyCashAccountService(db).GetByIdAsync(actor.Id, id); break;
                case "petty-transactions": await new PettyCashTransactionService(db).GetAllAsync(actor.Id, id); break;
                case "budget-lines": await new BudgetLineService(db).GetByBudgetAsync(actor.Id, id); break;
                case "invoice-lines": await new InvoiceLineService(db).GetByInvoiceAsync(actor.Id, id); break;
            }
        }
        await Read(ids[0]);
        Assert.NotNull(await Record.ExceptionAsync(() => Read(ids[1])));
        db.UserSiteAccesses.Single().IsActive = false; await db.SaveChangesAsync();
        Assert.NotNull(await Record.ExceptionAsync(() => Read(ids[0])));
    }
    [Theory]
    [InlineData("null")] [InlineData("other")] [InlineData("payroll")]
    public async Task Payment_creation_rejects_unscoped_targets_before_persisting(string target)
    {
        using var db = FinanceOfficerAuthorityTests.Database(); var actor = FinanceOfficerAuthorityTests.Actor(); db.Users.Add(actor);
        var site = new ConstructionSite { CompanyId = actor.CompanyId!.Value, Name = "Assigned", SiteCode = "A", IsActive = true };
        db.ConstructionSites.Add(site); db.UserSiteAccesses.Add(new() { CompanyId = site.CompanyId, UserId = actor.Id, SiteId = site.Id, IsActive = true });
        await db.SaveChangesAsync();
        var request = new CSM.Application.Finance.Payments.Dtos.CreatePaymentRequest(target == "null" ? null : target == "other" ? Guid.NewGuid() : site.Id,
            null, null, null, target == "payroll" ? Guid.NewGuid() : null, "P-1", new DateOnly(2026,11,11), PaymentDirection.Outgoing,
            10m, "ETB", 1m, "Cash", null, null);
        Assert.NotNull(await Record.ExceptionAsync(() => new PaymentService(db).CreateAsync(actor.Id, request)));
        Assert.Empty(db.Payments);
    }

    [Fact]
    public async Task Finance_officer_cannot_create_a_company_budget_without_a_site()
    {
        using var db = FinanceOfficerAuthorityTests.Database(); var actor = FinanceOfficerAuthorityTests.Actor(); db.Users.Add(actor); db.Companies.Add(new Company { Id = actor.CompanyId!.Value, Name = "Own", Code = "OWN", IsActive = true }); await db.SaveChangesAsync();
        var request = new CSM.Application.Finance.Budgets.Dtos.CreateBudgetRequest(null, null, "B-1", "Budget", null, 10m, "ETB", new DateOnly(2026,11,11), null);
        Assert.NotNull(await Record.ExceptionAsync(() => new BudgetService(db, new PermissionEvaluator(db)).CreateAsync(actor.Id, request)));
        Assert.Empty(db.Budgets);
    }

    [Theory]
    [InlineData("complete")] [InlineData("reverse")] [InlineData("change")]
    public async Task Payment_actions_cannot_mutate_unassigned_site_status(string action)
    {
        using var db = FinanceOfficerAuthorityTests.Database(); var actor = FinanceOfficerAuthorityTests.Actor(); db.Users.Add(actor);
        var payment = new Payment { CompanyId = actor.CompanyId!.Value, ConstructionSiteId = Guid.NewGuid(), PaymentNumber = "OTHER",
            Status = action == "reverse" ? PaymentStatus.Completed : PaymentStatus.Pending };
        db.Payments.Add(payment); await db.SaveChangesAsync(); var before = payment.Status;
        var service = new PaymentService(db);
        async Task Act() {
            if (action == "complete") await service.CompletePaymentAsync(actor.Id, payment.Id);
            else if (action == "reverse") await service.ReversePaymentAsync(actor.Id, payment.Id);
            else await service.ChangeStatusAsync(actor.Id, payment.Id, new CSM.Application.Finance.Payments.Dtos.ChangePaymentStatusRequest(PaymentStatus.Completed));
        }
        Assert.NotNull(await Record.ExceptionAsync(Act)); Assert.Equal(before, payment.Status);
    }

    [Fact]
    public async Task An_accountant_read_grant_does_not_widen_the_officers_budget_write_scope()
    {
        using var db = FinanceOfficerAuthorityTests.Database(); var actor = FinanceOfficerAuthorityTests.Actor();
        actor.UserRoles.Add(new UserRole { Role = FinanceOfficerAuthorityTests.Role("Accountant", GranularPermission.Finance_Budget_View) }); db.Users.Add(actor);
        db.Companies.Add(new Company { Id = actor.CompanyId!.Value, Code = "OWN", Name = "Own", IsActive = true });
        var budget = new Budget { CompanyId = actor.CompanyId.Value, BudgetNumber = "COMPANY", EffectiveFrom = new DateOnly(2026,11,11), Status = BudgetStatus.Draft }; db.Budgets.Add(budget); await db.SaveChangesAsync();
        var service = new BudgetService(db, new PermissionEvaluator(db));
        Assert.Single(await service.GetAllAsync(actor.Id)); Assert.Equal(budget.Id, (await service.GetByIdAsync(actor.Id, budget.Id)).Id);
        Assert.NotNull(await Record.ExceptionAsync(() => service.ChangeStatusAsync(actor.Id, budget.Id, new CSM.Application.Finance.Budgets.Dtos.ChangeBudgetStatusRequest(BudgetStatus.Approved))));
        Assert.Equal(BudgetStatus.Draft, budget.Status);
        Assert.Equal(2, actor.UserRoles.Count);
    }

    [Fact]
    public async Task Officer_without_any_site_assignment_cannot_load_invoice_vendor_options()
    {
        using var db = FinanceOfficerAuthorityTests.Database(); var actor = FinanceOfficerAuthorityTests.Actor(); db.Users.Add(actor);
        db.Vendors.Add(new Vendor { CompanyId = actor.CompanyId!.Value, VendorCode = "OWN", Name = "Own", IsActive = true }); await db.SaveChangesAsync();
        Assert.NotNull(await Record.ExceptionAsync(() => new InvoiceService(db).GetVendorOptionsAsync(actor.Id)));
    }

    [Theory]
    [InlineData("budget")] [InlineData("petty")]
    public async Task A_corporate_parent_read_grant_does_not_widen_write_form_reference_scope(string kind)
    {
        using var db = FinanceOfficerAuthorityTests.Database(); var actor = FinanceOfficerAuthorityTests.Actor();
        actor.UserRoles.Add(new() { Role = FinanceOfficerAuthorityTests.Role("Accountant", kind == "budget" ? GranularPermission.Finance_Budget_View : GranularPermission.Finance_PettyCash_View) }); db.Users.Add(actor);
        var budget = new Budget { CompanyId = actor.CompanyId!.Value, ConstructionSiteId = Guid.NewGuid(), BudgetNumber = "OTHER" }; db.Budgets.Add(budget);
        var cash = new PettyCashAccount { CompanyId = actor.CompanyId.Value, ConstructionSiteId = budget.ConstructionSiteId, AccountCode = "OTHER", AccountName = "Other" }; db.PettyCashAccounts.Add(cash);
        db.CostCodes.Add(new CostCode { CompanyId = actor.CompanyId.Value, Code = "SHARED", Name = "Shared", IsActive = true }); await db.SaveChangesAsync();
        async Task Read() { if (kind == "budget") await new BudgetLineService(db).GetCostCodeOptionsAsync(actor.Id, budget.Id); else await new PettyCashTransactionService(db).GetCostCodeOptionsAsync(actor.Id, cash.Id); }
        Assert.NotNull(await Record.ExceptionAsync(Read));
    }

}
