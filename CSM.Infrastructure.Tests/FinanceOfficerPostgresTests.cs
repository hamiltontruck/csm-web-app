using System.Reflection;
using CSM.Application.Finance.Budgets.Dtos;
using CSM.Application.Finance.BudgetLines.Dtos;
using CSM.Application.Finance.Invoices.Dtos;
using CSM.Application.Finance.InvoiceLines.Dtos;
using CSM.Application.Finance.Payments.Dtos;
using CSM.Application.Finance.Expenses.Dtos;
using CSM.Application.Finance.PettyCashAccounts.Dtos;
using CSM.Application.Finance.PettyCashTransactions.Dtos;
using CSM.Domain.Entities.Finance;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Entities.Projects;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Security;
using CSM.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace CSM.Infrastructure.Tests;
public sealed class FinanceOfficerPostgresTests
{
    [Fact]
    public async Task Seeded_officer_completes_site_finance_lifecycle_with_persisted_balances_and_revoked_assignment_blocks_further_actions()
    {
        var connection = new Npgsql.NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("CSM_TEST_POSTGRES") ?? "Host=localhost;Username=postgres;Password=postgres") { Database = "finance_officer_" + Guid.NewGuid().ToString("N") };
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection.ConnectionString).Options;
        await using var db = new ApplicationDbContext(options);
        try {
            await db.Database.MigrateAsync();
            foreach (var method in new[] { "SeedRolesAsync", "SeedGranularRolePermissionsAsync" })
                await (Task)typeof(DatabaseInitializer).GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [db, CancellationToken.None])!;
            var company = new Company { Code = "OWN", Name = "Own", IsActive = true }; db.Companies.Add(company);
            var site = new ConstructionSite { CompanyId = company.Id, SiteCode = "A", Name = "Assigned", IsActive = true }; db.ConstructionSites.Add(site);
            var project = new Project { CompanyId = company.Id, ConstructionSiteId = site.Id, ProjectCode = "P", Name = "Project" }; db.Projects.Add(project);
            var code = new CostCode { CompanyId = company.Id, Code = "SHARED", Name = "Shared", IsActive = true }; db.CostCodes.Add(code);
            var actor = new User { CompanyId = company.Id, Email = "finance@example.test", NormalizedEmail = "FINANCE@EXAMPLE.TEST", IsActive = true,
                UserRoles = [new() { Role = await db.Roles.SingleAsync(x => x.Name == "FinanceOfficer") }] }; db.Users.Add(actor);
            var assignment = new UserSiteAccess { UserId = actor.Id, CompanyId = company.Id, SiteId = site.Id, IsActive = true }; db.UserSiteAccesses.Add(assignment);
            await db.SaveChangesAsync(); var date = new DateOnly(2026,11,11);
            var budgets = new BudgetService(db, new PermissionEvaluator(db));
            var budget = await budgets.CreateAsync(actor.Id, new CreateBudgetRequest(site.Id, project.Id, "B-1", "Budget", null, 10m, "ETB", date, null));
            await new BudgetLineService(db).CreateAsync(actor.Id, new CreateBudgetLineRequest(budget.Id, code.Id, "Line", 10m, 10m));
            await budgets.ChangeStatusAsync(actor.Id, budget.Id, new ChangeBudgetStatusRequest(BudgetStatus.PendingApproval));
            await budgets.ChangeStatusAsync(actor.Id, budget.Id, new ChangeBudgetStatusRequest(BudgetStatus.Approved));
            var invoices = new InvoiceService(db);
            var invoice = await invoices.CreateAsync(actor.Id, new CreateInvoiceRequest(site.Id, project.Id, null, "I-1", InvoiceType.ClientInvoice, date, null, 1m, 0m, 1m, "ETB", 1m, null, null));
            await new InvoiceLineService(db).CreateAsync(actor.Id, invoice.Id, new CreateInvoiceLineRequest(code.Id, "Line", 1m, "Unit", 1m, 0m));
            await invoices.IssueAsync(actor.Id, invoice.Id);
            var payments = new PaymentService(db);
            var payment = await payments.CreateAsync(actor.Id, new CreatePaymentRequest(site.Id, project.Id, invoice.Id, null, null, "P-1", date, PaymentDirection.Incoming, 1m, "ETB", 1m, "Cash", null, null));
            await payments.CompletePaymentAsync(actor.Id, payment.Id);
            Assert.Equal(InvoiceStatus.Paid, (await db.Invoices.AsNoTracking().SingleAsync(x => x.Id == invoice.Id)).Status);
            await payments.ReversePaymentAsync(actor.Id, payment.Id);
            Assert.Equal(0m, (await invoices.GetByIdAsync(actor.Id, invoice.Id)).PaidAmount);
            var expenses = new ExpenseService(db);
            var expense = await expenses.CreateAsync(actor.Id, new CreateExpenseRequest(site.Id, project.Id, code.Id, null, "E-1", date, "Expense", 1m, 0m, "ETB", 1m, null, null));
            await expenses.ChangeStatusAsync(actor.Id, expense.Id, new ChangeExpenseStatusRequest(ExpenseStatus.Submitted));
            await expenses.ChangeStatusAsync(actor.Id, expense.Id, new ChangeExpenseStatusRequest(ExpenseStatus.Approved));
            var cash = await new PettyCashAccountService(db).CreateAsync(actor.Id, new CreatePettyCashAccountRequest { ConstructionSiteId = site.Id, AccountCode = "PC-1", AccountName = "Cash", OpeningBalance = 10m });
            await new PettyCashTransactionService(db).CreateAsync(actor.Id, new CreatePettyCashTransactionRequest { PettyCashAccountId = cash.Id, CostCodeId = code.Id, TransactionType = "CashOut", ReferenceNumber = "PC-OUT-1", Description = "Expense", Amount = 1m });
            await using var verify = new ApplicationDbContext(options);
            Assert.Equal(9m, (await verify.PettyCashAccounts.SingleAsync(x => x.Id == cash.Id)).CurrentBalance);
            Assert.Equal(ExpenseStatus.Approved, (await verify.Expenses.SingleAsync(x => x.Id == expense.Id)).Status);
            Assert.Single(await new CostCodeService(db).GetAllAsync(actor.Id));
            Assert.Single(await new SiteService(db).GetAllAsync(actor.Id));
            var otherSite = new ConstructionSite { CompanyId = company.Id, SiteCode = "OTHER", Name = "Unassigned", IsActive = true }; db.ConstructionSites.Add(otherSite);
            var otherInvoice = new Invoice { CompanyId = company.Id, ConstructionSiteId = otherSite.Id, InvoiceNumber = "OTHER-I", InvoiceDate = date, Type = InvoiceType.ClientInvoice, Status = InvoiceStatus.Issued, TotalAmount = 1m, CurrencyCode = "ETB", ExchangeRate = 1m }; db.Invoices.Add(otherInvoice);
            var otherPayment = new Payment { CompanyId = company.Id, ConstructionSiteId = otherSite.Id, InvoiceId = otherInvoice.Id, PaymentNumber = "OTHER-P", PaymentDate = date, Direction = PaymentDirection.Incoming, Amount = 1m, CurrencyCode = "ETB", ExchangeRate = 1m, Status = PaymentStatus.Pending }; db.Payments.Add(otherPayment);
            var otherCash = new PettyCashAccount { CompanyId = company.Id, ConstructionSiteId = otherSite.Id, AccountCode = "OTHER-PC", AccountName = "Other", OpeningBalance = 10m, CurrentBalance = 10m, IsActive = true }; db.PettyCashAccounts.Add(otherCash);
            await db.SaveChangesAsync();
            Assert.NotNull(await Record.ExceptionAsync(() => payments.CompletePaymentAsync(actor.Id, otherPayment.Id)));
            Assert.NotNull(await Record.ExceptionAsync(() => new PettyCashTransactionService(db).CreateAsync(actor.Id, new CreatePettyCashTransactionRequest { PettyCashAccountId = otherCash.Id, TransactionType = "CashOut", Amount = 1m, ReferenceNumber = "DENIED", Description = "Denied" })));
            Assert.Equal(PaymentStatus.Pending, (await verify.Payments.AsNoTracking().SingleAsync(x => x.Id == otherPayment.Id)).Status);
            Assert.Equal(InvoiceStatus.Issued, (await verify.Invoices.AsNoTracking().SingleAsync(x => x.Id == otherInvoice.Id)).Status);
            Assert.Equal(0m, (await verify.Invoices.AsNoTracking().SingleAsync(x => x.Id == otherInvoice.Id)).PaidAmount);
            Assert.Equal(10m, (await verify.PettyCashAccounts.AsNoTracking().SingleAsync(x => x.Id == otherCash.Id)).CurrentBalance);
            assignment.IsActive = false; await db.SaveChangesAsync();
            Assert.Empty(await budgets.GetAllAsync(actor.Id)); Assert.Empty(await payments.GetAllAsync(actor.Id));
            Assert.NotNull(await Record.ExceptionAsync(() => payments.CompletePaymentAsync(actor.Id, payment.Id)));
            Assert.Equal(PaymentStatus.Reversed, (await verify.Payments.AsNoTracking().SingleAsync(x => x.Id == payment.Id)).Status);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
