using CSM.Application.Finance.BudgetLines.Dtos;
using CSM.Application.Finance.InvoiceLines.Dtos;
using CSM.Application.Finance.Invoices.Dtos;
using CSM.Application.Finance.PettyCashAccounts.Dtos;
using CSM.Domain.Entities.Finance;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Entities.Projects;
using CSM.Domain.Enums;
using CSM.Infrastructure.Services;
using Xunit;
namespace CSM.Infrastructure.Tests;
public sealed class FinanceOfficerReferenceTests
{
    [Theory]
    [InlineData("budget-costcode")] [InlineData("invoice-costcode")] [InlineData("invoice-project")]
    [InlineData("inactive-petty-create")] [InlineData("inactive-petty-update")] [InlineData("budget-options")] [InlineData("expense-costcode")]
    public async Task Finance_targets_and_lookup_options_cannot_cross_the_selected_site(string kind)
    {
        using var db = FinanceOfficerAuthorityTests.Database(); var actor = FinanceOfficerAuthorityTests.Actor(); db.Users.Add(actor); var company = actor.CompanyId!.Value;
        db.Companies.Add(new Company { Id = company, Code = "OWN", Name = "Own", IsActive = true });
        var site = new ConstructionSite { CompanyId = company, Name = "Assigned", SiteCode = "A", IsActive = !kind.StartsWith("inactive") };
        db.ConstructionSites.Add(site); db.UserSiteAccesses.Add(new() { UserId = actor.Id, CompanyId = company, SiteId = site.Id, IsActive = true });
        var other = Guid.NewGuid();
        var code = new CostCode { CompanyId = company, ConstructionSiteId = other, Code = "OTHER", Name = "Other" }; db.CostCodes.Add(code);
        var project = new Project { CompanyId = company, ConstructionSiteId = other, Name = "Other", ProjectCode = "OTHER" }; db.Projects.Add(project);
        var budget = new Budget { CompanyId = company, ConstructionSiteId = site.Id, BudgetNumber = "B-1", EffectiveFrom = new DateOnly(2026,11,11) }; db.Budgets.Add(budget);
        var invoice = new Invoice { CompanyId = company, ConstructionSiteId = site.Id, InvoiceNumber = "I-1" }; db.Invoices.Add(invoice);
        var account = new PettyCashAccount { CompanyId = company, ConstructionSiteId = site.Id, AccountCode = "PC-1", AccountName = "Account", IsActive = true }; db.PettyCashAccounts.Add(account);
        await db.SaveChangesAsync();
        if (kind == "budget-options")
        {
            Assert.DoesNotContain(await new BudgetLineService(db).GetCostCodeOptionsAsync(actor.Id, budget.Id), x => x.Id == code.Id); return;
        }
        async Task Act() {
            switch (kind) {
                case "expense-costcode": await new ExpenseService(db).CreateAsync(actor.Id, new CSM.Application.Finance.Expenses.Dtos.CreateExpenseRequest(site.Id, null, code.Id, null, "E-1", new DateOnly(2026,11,11), "Expense", 1m, 0m, "ETB", 1m, null, null)); break;
                case "budget-costcode": await new BudgetLineService(db).CreateAsync(actor.Id, new CreateBudgetLineRequest(budget.Id, code.Id, "Line", 1m, 1m)); break;
                case "invoice-costcode": await new InvoiceLineService(db).CreateAsync(actor.Id, invoice.Id, new CreateInvoiceLineRequest(code.Id, "Line", 1m, "Unit", 1m, 0m)); break;
                case "invoice-project": await new InvoiceService(db).CreateAsync(actor.Id, new CreateInvoiceRequest(site.Id, project.Id, null, "I-2", InvoiceType.ClientInvoice, new DateOnly(2026,11,11), null, 1m, 0m, 1m, "ETB", 1m, null, null)); break;
                case "inactive-petty-create": await new PettyCashAccountService(db).CreateAsync(actor.Id, new CreatePettyCashAccountRequest { ConstructionSiteId = site.Id, AccountCode = "PC-2", AccountName = "New", OpeningBalance = 1m }); break;
                case "inactive-petty-update": await new PettyCashAccountService(db).UpdateAsync(actor.Id, account.Id, new UpdatePettyCashAccountRequest { AccountName = "Changed", IsActive = true }); break;
            }
        }
        Assert.NotNull(await Record.ExceptionAsync(Act));
        Assert.Empty(db.Expenses); Assert.Empty(db.BudgetLines); Assert.Empty(db.InvoiceLines); Assert.Single(db.Invoices); Assert.Single(db.PettyCashAccounts);
        Assert.Equal("Account", account.AccountName);
    }
}
