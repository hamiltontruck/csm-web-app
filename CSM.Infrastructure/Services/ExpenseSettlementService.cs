using CSM.Application.Common.Exceptions;
using CSM.Application.Finance.Expenses;
using CSM.Application.Finance.Expenses.Dtos;
using CSM.Domain.Entities.Finance;
using CSM.Domain.Enums;
using CSM.Domain.Security;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public sealed class ExpenseSettlementService(ApplicationDbContext db) : IExpenseSettlementService
{
    private static ExpenseManagementException Error(string message) => new(message);

    // Each action resolves its own live authority. Corporate authority for a different
    // action cannot widen this action's assigned-site scope.
    private async Task<IQueryable<Expense>> ScopeAsync(Guid userId, string purpose, CancellationToken ct, Guid? requiredSite = null)
    {
        var actor = await db.Users.AsNoTracking().Include(x => x.UserRoles).ThenInclude(x => x.Role)
            .ThenInclude(x => x.GranularPermissions).SingleOrDefaultAsync(x => x.Id == userId && x.IsActive && !x.IsDeleted, ct)
            ?? throw Error("Current user is inactive or unavailable.");
        var actions = purpose switch
        {
            "payment" => new[] { GranularPermission.Finance_Expense_View, GranularPermission.Finance_Payment_View, GranularPermission.Finance_Payment_Manage },
            "petty-cash" => new[] { GranularPermission.Finance_Expense_View, GranularPermission.Finance_PettyCash_View, GranularPermission.Finance_PettyCash_Manage },
            _ => throw Error("Unknown settlement purpose.")
        };
        var selectedSite = requiredSite.HasValue ? await db.ConstructionSites.AsNoTracking().SingleOrDefaultAsync(x => x.Id == requiredSite && !x.IsDeleted, ct) : null;
        if (requiredSite.HasValue && selectedSite is null) throw Error("The selected site was not found.");
        IQueryable<Expense> query = db.Expenses.Where(x => !x.IsDeleted);
        foreach (var action in actions)
        {
            var supported = action == GranularPermission.Finance_Expense_View
                ? new[] { "SuperAdmin", "CompanyAdmin", "Accountant", "FinanceOfficer", "SiteManager" }
                : new[] { "SuperAdmin", "CompanyAdmin", "Accountant", "FinanceOfficer" };
            var roles = actor.UserRoles.Where(x => !x.IsDeleted && !x.Role.IsDeleted &&
                supported.Contains(x.Role.Name, StringComparer.OrdinalIgnoreCase) && RolePermissionRules.Allows(x.Role.Name, action.ToString()) &&
                x.Role.GranularPermissions.Any(p => p.Permission == action)).Select(x => x.Role.Name).ToArray();
            if (roles.Length == 0) throw Error("Expense reading and disbursement permissions are required.");
            if (roles.Contains("SuperAdmin", StringComparer.OrdinalIgnoreCase)) continue;
            if (actor.CompanyId is not Guid company) throw Error("A company context is required.");
            if (selectedSite is not null && selectedSite.CompanyId != company) throw Error("The selected site is outside your company.");
            query = query.Where(x => x.CompanyId == company);
            if (roles.Any(x => new[] { "CompanyAdmin", "Accountant" }.Contains(x, StringComparer.OrdinalIgnoreCase))) continue;
            var sites = db.ConstructionSites.Where(s => s.CompanyId == company && s.IsActive && !s.IsDeleted &&
                db.UserSiteAccesses.Any(a => a.UserId == userId && a.CompanyId == company && a.SiteId == s.Id && a.IsActive && !a.IsDeleted)).Select(s => s.Id);
            if (requiredSite.HasValue && !await sites.AnyAsync(x => x == requiredSite, ct)) throw Error("The selected site is outside your current assigned scope.");
            query = query.Where(x => x.ConstructionSiteId.HasValue && sites.Contains(x.ConstructionSiteId.Value));
        }
        return query;
    }

    internal async Task<Expense> LoadForDisbursementAsync(Guid userId, Guid expenseId, string purpose, CancellationToken ct)
    {
        var scope = await ScopeAsync(userId, purpose, ct);
        return await scope.SingleOrDefaultAsync(x => x.Id == expenseId, ct) ?? throw Error("Expense was not found in your current settlement scope.");
    }

    public async Task<IReadOnlyList<ExpenseSettlementOption>> GetOptionsAsync(Guid userId, string purpose, Guid? siteId, Guid? accountId, CancellationToken ct = default)
    {
        var scope = await ScopeAsync(userId, purpose, ct);
        if (accountId.HasValue)
        {
            var account = await db.PettyCashAccounts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == accountId && x.IsActive && !x.IsDeleted, ct)
                ?? throw Error("An active cash account is required.");
            if (siteId.HasValue && account.ConstructionSiteId != siteId) throw Error("Cash account does not match the selected site.");
            siteId = account.ConstructionSiteId;
            scope = scope.Where(x => x.CompanyId == account.CompanyId && x.ConstructionSiteId == account.ConstructionSiteId);
        }
        if (siteId.HasValue)
        {
            _ = await ScopeAsync(userId, purpose, ct, siteId);
            scope = scope.Where(x => x.ConstructionSiteId == siteId);
        }
        // Never expose all companies to a companyless administrator without context.
        var companyId = await db.Users.Where(x => x.Id == userId).Select(x => x.CompanyId).SingleAsync(ct);
        if (!companyId.HasValue && !siteId.HasValue && !accountId.HasValue) throw Error("Select a site or expense company context first.");
        var candidates = await scope.AsNoTracking().Where(x => x.Status == ExpenseStatus.Approved).OrderBy(x => x.ExpenseNumber).ToListAsync(ct);
        var result = new List<ExpenseSettlementOption>();
        foreach (var expense in candidates)
        {
            if (purpose == "petty-cash" && (expense.CurrencyCode != "ETB" || expense.ExchangeRate != 1m)) continue;
            var summary = await GetSummaryAsync(expense, ct);
            if (summary.OutstandingAmount is > 0) result.Add(await OptionAsync(expense, summary, ct));
        }
        return result;
    }

    public async Task<ExpenseSettlementOptionsResponse> GetForExpenseAsync(Guid userId, Guid expenseId, string purpose, CancellationToken ct = default)
    {
        var expense = await LoadForDisbursementAsync(userId, expenseId, purpose, ct);
        var summary = await GetSummaryAsync(expense, ct);
        if (expense.Status != ExpenseStatus.Approved || summary.OutstandingAmount is not > 0) throw Error("Expense is not available for a new disbursement.");
        if (purpose == "petty-cash" && (expense.CurrencyCode != "ETB" || expense.ExchangeRate != 1m)) throw Error("Petty cash supports ETB at exchange rate 1 only.");
        var accounts = purpose == "petty-cash" ? await db.PettyCashAccounts.AsNoTracking().Where(x => !x.IsDeleted && x.IsActive &&
            x.CompanyId == expense.CompanyId && x.ConstructionSiteId == expense.ConstructionSiteId)
            .Select(x => new ExpenseCashAccountOption(x.Id, x.AccountCode, x.AccountName, x.CurrentBalance)).ToListAsync(ct) : [];
        return new(await OptionAsync(expense, summary, ct), accounts);
    }

    private async Task<ExpenseSettlementOption> OptionAsync(Expense expense, ExpenseSettlementSummaryResponse summary, CancellationToken ct) =>
        new(expense.Id, expense.ExpenseNumber, expense.CompanyId, expense.ConstructionSiteId, expense.ProjectId, expense.VendorId,
            expense.VendorId.HasValue ? await db.Vendors.Where(x => x.Id == expense.VendorId && x.CompanyId == expense.CompanyId && !x.IsDeleted).Select(x => x.Name).SingleOrDefaultAsync(ct) : null,
            expense.CostCodeId, expense.CurrencyCode, expense.ExchangeRate, summary.TotalDue, summary.OutstandingAmount!.Value);

    internal async Task<ExpenseSettlementSummaryResponse> GetSummaryAsync(Expense expense, CancellationToken ct)
    {
        var payments = await db.Payments.AsNoTracking().Where(x => !x.IsDeleted && x.ExpenseId == expense.Id && x.InvoiceId == null && x.PayrollRecordId == null &&
            x.CompanyId == expense.CompanyId && x.ConstructionSiteId == expense.ConstructionSiteId && x.ProjectId == expense.ProjectId && x.VendorId == expense.VendorId &&
            x.CurrencyCode == expense.CurrencyCode && x.ExchangeRate == expense.ExchangeRate && x.Direction == PaymentDirection.Outgoing && x.Status == PaymentStatus.Completed && x.Amount > 0)
            .Select(x => new ExpenseSettlementEvidence("Payment", x.Id, x.PaymentNumber, "Completed", x.Amount, x.CurrencyCode)).ToListAsync(ct);
        var cash = expense.CurrencyCode == "ETB" && expense.ExchangeRate == 1m
            ? await db.PettyCashTransactions.AsNoTracking().Where(x => !x.IsDeleted && x.ExpenseId == expense.Id && x.TransactionType == "CashOut" && x.IsApproved && x.Amount > 0 &&
                x.CostCodeId == expense.CostCodeId && !x.PettyCashAccount.IsDeleted && x.PettyCashAccount.CompanyId == expense.CompanyId && x.PettyCashAccount.ConstructionSiteId == expense.ConstructionSiteId)
                .Select(x => new ExpenseSettlementEvidence("PettyCash", x.Id, x.ReferenceNumber, "Approved", x.Amount, "ETB")).ToListAsync(ct) : [];
        var evidence = payments.Concat(cash).ToArray();
        var due = expense.Amount + expense.TaxAmount;
        var settled = evidence.Sum(x => x.Amount);
        var legacy = expense.Status == ExpenseStatus.Paid && settled < due;
        return new(due, settled, legacy ? null : Math.Max(0m, due - settled), !legacy, legacy,
            legacy ? "LegacyPaid" : settled >= due && due > 0 ? "Paid" : settled > 0 ? "Partial" : "Unpaid", evidence);
    }

    internal async Task ValidateNewSettlementAsync(Expense expense, decimal amount, string currency, decimal rate, bool petty, CancellationToken ct)
    {
        if (expense.Status != ExpenseStatus.Approved || !expense.ApprovedBy.HasValue) throw Error("Only an approved expense can receive a new disbursement.");
        // Independence is enforced by new approval transitions. Existing Approved
        // obligations retain historical approval eligibility without rewriting provenance.
        if (amount <= 0 || amount >= 1000000000000000m || decimal.Round(amount, 4) != amount) throw Error("Amount must be positive with at most four decimal places.");
        if (currency != expense.CurrencyCode || rate != expense.ExchangeRate || (petty && (currency != "ETB" || rate != 1m))) throw Error("Disbursement currency and exchange rate must match the expense. Petty cash supports ETB at rate 1 only.");
        var summary = await GetSummaryAsync(expense, ct);
        if (!summary.IsVerified || amount > summary.OutstandingAmount) throw Error("Amount exceeds the expense's remaining payable amount.");
    }

    internal async Task SynchronizeAsync(Expense expense, CancellationToken ct)
    {
        var summary = await GetSummaryAsync(expense, ct);
        if (expense.Status is ExpenseStatus.Approved or ExpenseStatus.Paid)
            expense.Status = summary.SettledAmount >= summary.TotalDue && summary.TotalDue > 0 ? ExpenseStatus.Paid : ExpenseStatus.Approved;
    }
}
