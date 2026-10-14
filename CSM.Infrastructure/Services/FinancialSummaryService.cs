using CSM.Application.Finance.Summaries;
using CSM.Application.Finance.Summaries.Dtos;
using CSM.Domain.Entities;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public sealed class FinancialSummaryService : IFinancialSummaryService
{
    private readonly ApplicationDbContext _dbContext;

    public FinancialSummaryService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CompanyFinancialSummaryResponse> GetCompanySummaryAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var actor = await _dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == currentUserId,
                cancellationToken)
            ?? throw new UnauthorizedAccessException();

        if (actor.CompanyId == null)
        {
            throw new InvalidOperationException(
                "The current user is not associated with a company.");
        }

        var companyId = actor.CompanyId.Value;

        // ============================================================
        // COMPANY TOTALS
        // Includes company-level records AND all site-level records.
        // ============================================================

        var totalBudget = await _dbContext.Budgets
            .Where(x =>
                x.CompanyId == companyId &&
                x.Status != BudgetStatus.Cancelled &&
                x.Status != BudgetStatus.Draft &&
                !x.IsDeleted)
            .SumAsync(
                x => x.TotalAmount,
                cancellationToken);

        var totalExpenses = await _dbContext.Expenses
            .Where(x =>
                x.CompanyId == companyId &&
                (x.Status == ExpenseStatus.Approved ||
                 x.Status == ExpenseStatus.Paid) &&
                !x.IsDeleted)
            .SumAsync(
                x => x.Amount + x.TaxAmount,
                cancellationToken);

        var totalInvoiceAmount = await _dbContext.Invoices
            .Where(x =>
                x.CompanyId == companyId &&
                x.Status != InvoiceStatus.Draft &&
                x.Status != InvoiceStatus.Cancelled &&
                !x.IsDeleted)
            .SumAsync(
                x => x.TotalAmount,
                cancellationToken);

        var totalPayments = await _dbContext.Payments
            .Where(x =>
                x.CompanyId == companyId &&
                x.Status == PaymentStatus.Completed &&
                !x.IsDeleted)
            .SumAsync(
                x => x.Amount,
                cancellationToken);

        var totalPettyCashBalance = await _dbContext.PettyCashAccounts
            .Where(x =>
                x.CompanyId == companyId &&
                x.IsActive)
            .SumAsync(
                x => x.CurrentBalance,
                cancellationToken);

        // ============================================================
        // COMPANY RECEIVABLES
        // Client invoices minus completed incoming invoice payments.
        // ============================================================

        var outstandingReceivables =
            await _dbContext.Invoices
                .Where(x =>
                    x.CompanyId == companyId &&
                    x.Type == InvoiceType.ClientInvoice &&
                    x.Status != InvoiceStatus.Draft &&
                    x.Status != InvoiceStatus.Cancelled &&
                    !x.IsDeleted)
                .SumAsync(
                    x => x.TotalAmount,
                    cancellationToken)
            -
            await _dbContext.Payments
                .Where(x =>
                    x.CompanyId == companyId &&
                    x.Direction == PaymentDirection.Incoming &&
                    x.Status == PaymentStatus.Completed &&
                    x.InvoiceId != null &&
                    !x.IsDeleted)
                .SumAsync(
                    x => x.Amount,
                    cancellationToken);

        // ============================================================
        // COMPANY PAYABLES
        // Vendor bills minus completed outgoing invoice payments.
        // ============================================================

        var outstandingPayables =
            await _dbContext.Invoices
                .Where(x =>
                    x.CompanyId == companyId &&
                    x.Type == InvoiceType.VendorBill &&
                    x.Status != InvoiceStatus.Draft &&
                    x.Status != InvoiceStatus.Cancelled &&
                    !x.IsDeleted)
                .SumAsync(
                    x => x.TotalAmount,
                    cancellationToken)
            -
            await _dbContext.Payments
                .Where(x =>
                    x.CompanyId == companyId &&
                    x.Direction == PaymentDirection.Outgoing &&
                    x.Status == PaymentStatus.Completed &&
                    x.InvoiceId != null &&
                    !x.IsDeleted)
                .SumAsync(
                    x => x.Amount,
                    cancellationToken);

        // ============================================================
        // COMPANY NET CASH FLOW
        // Completed incoming cash minus completed outgoing cash.
        // ============================================================

        var incomingCash = await _dbContext.Payments
            .Where(x =>
                x.CompanyId == companyId &&
                x.Direction == PaymentDirection.Incoming &&
                x.Status == PaymentStatus.Completed &&
                !x.IsDeleted)
            .SumAsync(
                x => x.Amount,
                cancellationToken);

        var outgoingCash = await _dbContext.Payments
            .Where(x =>
                x.CompanyId == companyId &&
                x.Direction == PaymentDirection.Outgoing &&
                x.Status == PaymentStatus.Completed &&
                !x.IsDeleted)
            .SumAsync(
                x => x.Amount,
                cancellationToken);

        var netCashFlow = incomingCash - outgoingCash;

        // ============================================================
        // SITE BREAKDOWN
        // Each site contains ONLY its own records.
        // ============================================================

        var sites = await _dbContext.ConstructionSites
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == companyId &&
                !x.IsDeleted)
            .ToListAsync(cancellationToken);

        var siteSummaries =
            new List<SiteFinancialSummaryResponse>();

        foreach (var site in sites)
        {
            var budget = await _dbContext.Budgets
                .Where(x =>
                    x.CompanyId == companyId &&
                    x.ConstructionSiteId == site.Id &&
                    x.Status != BudgetStatus.Cancelled &&
                    x.Status != BudgetStatus.Draft &&
                    !x.IsDeleted)
                .SumAsync(
                    x => x.TotalAmount,
                    cancellationToken);

            var expenses = await _dbContext.Expenses
                .Where(x =>
                    x.CompanyId == companyId &&
                    x.ConstructionSiteId == site.Id &&
                    (x.Status == ExpenseStatus.Approved ||
                     x.Status == ExpenseStatus.Paid) &&
                    !x.IsDeleted)
                .SumAsync(
                    x => x.Amount + x.TaxAmount,
                    cancellationToken);

            var invoices = await _dbContext.Invoices
                .Where(x =>
                    x.CompanyId == companyId &&
                    x.ConstructionSiteId == site.Id &&
                    x.Status != InvoiceStatus.Draft &&
                    x.Status != InvoiceStatus.Cancelled &&
                    !x.IsDeleted)
                .SumAsync(
                    x => x.TotalAmount,
                    cancellationToken);

            var payments = await _dbContext.Payments
                .Where(x =>
                    x.CompanyId == companyId &&
                    x.ConstructionSiteId == site.Id &&
                    x.Status == PaymentStatus.Completed &&
                    !x.IsDeleted)
                .SumAsync(
                    x => x.Amount,
                    cancellationToken);

            var pettyCash = await _dbContext.PettyCashAccounts
                .Where(x =>
                    x.CompanyId == companyId &&
                    x.ConstructionSiteId == site.Id &&
                    x.IsActive)
                .SumAsync(
                    x => x.CurrentBalance,
                    cancellationToken);

            // --------------------------------------------------------
            // SITE RECEIVABLES
            // --------------------------------------------------------

            var siteOutstandingReceivables =
                await _dbContext.Invoices
                    .Where(x =>
                        x.CompanyId == companyId &&
                        x.ConstructionSiteId == site.Id &&
                        x.Type == InvoiceType.ClientInvoice &&
                        x.Status != InvoiceStatus.Draft &&
                        x.Status != InvoiceStatus.Cancelled &&
                        !x.IsDeleted)
                    .SumAsync(
                        x => x.TotalAmount,
                        cancellationToken)
                -
                await _dbContext.Payments
                    .Where(x =>
                        x.CompanyId == companyId &&
                        x.ConstructionSiteId == site.Id &&
                        x.Direction == PaymentDirection.Incoming &&
                        x.Status == PaymentStatus.Completed &&
                        x.InvoiceId != null &&
                        !x.IsDeleted)
                    .SumAsync(
                        x => x.Amount,
                        cancellationToken);

            // --------------------------------------------------------
            // SITE PAYABLES
            // --------------------------------------------------------

            var siteOutstandingPayables =
                await _dbContext.Invoices
                    .Where(x =>
                        x.CompanyId == companyId &&
                        x.ConstructionSiteId == site.Id &&
                        x.Type == InvoiceType.VendorBill &&
                        x.Status != InvoiceStatus.Draft &&
                        x.Status != InvoiceStatus.Cancelled &&
                        !x.IsDeleted)
                    .SumAsync(
                        x => x.TotalAmount,
                        cancellationToken)
                -
                await _dbContext.Payments
                    .Where(x =>
                        x.CompanyId == companyId &&
                        x.ConstructionSiteId == site.Id &&
                        x.Direction == PaymentDirection.Outgoing &&
                        x.Status == PaymentStatus.Completed &&
                        x.InvoiceId != null &&
                        !x.IsDeleted)
                    .SumAsync(
                        x => x.Amount,
                        cancellationToken);

            // --------------------------------------------------------
            // SITE NET CASH FLOW
            // --------------------------------------------------------

            var siteIncomingCash = await _dbContext.Payments
                .Where(x =>
                    x.CompanyId == companyId &&
                    x.ConstructionSiteId == site.Id &&
                    x.Direction == PaymentDirection.Incoming &&
                    x.Status == PaymentStatus.Completed &&
                    !x.IsDeleted)
                .SumAsync(
                    x => x.Amount,
                    cancellationToken);

            var siteOutgoingCash = await _dbContext.Payments
                .Where(x =>
                    x.CompanyId == companyId &&
                    x.ConstructionSiteId == site.Id &&
                    x.Direction == PaymentDirection.Outgoing &&
                    x.Status == PaymentStatus.Completed &&
                    !x.IsDeleted)
                .SumAsync(
                    x => x.Amount,
                    cancellationToken);

            var siteNetCashFlow =
                siteIncomingCash - siteOutgoingCash;

            siteSummaries.Add(
                new SiteFinancialSummaryResponse
                {
                    ConstructionSiteId = site.Id,
                    ConstructionSiteName = site.Name,
                    BudgetAmount = budget,
                    ExpenseAmount = expenses,
                    InvoiceAmount = invoices,
                    PaymentAmount = payments,
                    PettyCashBalance = pettyCash,
                    OutstandingReceivables =
                        siteOutstandingReceivables,
                    OutstandingPayables =
                        siteOutstandingPayables,
                    NetCashFlow =
                        siteNetCashFlow
                });
        }

        return new CompanyFinancialSummaryResponse
        {
            TotalBudget = totalBudget,
            TotalExpenses = totalExpenses,
            TotalInvoiceAmount = totalInvoiceAmount,
            TotalPayments = totalPayments,
            TotalPettyCashBalance = totalPettyCashBalance,
            OutstandingReceivables = outstandingReceivables,
            OutstandingPayables = outstandingPayables,
            NetCashFlow = netCashFlow,
            Sites = siteSummaries
        };
    }

    public async Task<SiteFinancialSummaryResponse> GetSiteSummaryAsync(
        Guid currentUserId,
        Guid constructionSiteId,
        CancellationToken cancellationToken = default)
    {
        var actor = await _dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == currentUserId,
                cancellationToken)
            ?? throw new UnauthorizedAccessException();

        if (actor.CompanyId == null)
        {
            throw new InvalidOperationException(
                "The current user is not associated with a company.");
        }

        var companyId = actor.CompanyId.Value;

        var site = await _dbContext.ConstructionSites
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x =>
                    x.Id == constructionSiteId &&
                    x.CompanyId == companyId &&
                    !x.IsDeleted,
                cancellationToken)
            ?? throw new KeyNotFoundException(
                "Construction site not found.");

        var budget = await _dbContext.Budgets
            .Where(x =>
                x.CompanyId == companyId &&
                x.ConstructionSiteId == constructionSiteId &&
                x.Status != BudgetStatus.Cancelled &&
                x.Status != BudgetStatus.Draft &&
                !x.IsDeleted)
            .SumAsync(
                x => x.TotalAmount,
                cancellationToken);

        var expenses = await _dbContext.Expenses
            .Where(x =>
                x.CompanyId == companyId &&
                x.ConstructionSiteId == constructionSiteId &&
                (x.Status == ExpenseStatus.Approved ||
                 x.Status == ExpenseStatus.Paid) &&
                !x.IsDeleted)
            .SumAsync(
                x => x.Amount + x.TaxAmount,
                cancellationToken);

        var invoices = await _dbContext.Invoices
            .Where(x =>
                x.CompanyId == companyId &&
                x.ConstructionSiteId == constructionSiteId &&
                x.Status != InvoiceStatus.Draft &&
                x.Status != InvoiceStatus.Cancelled &&
                !x.IsDeleted)
            .SumAsync(
                x => x.TotalAmount,
                cancellationToken);

        var payments = await _dbContext.Payments
            .Where(x =>
                x.CompanyId == companyId &&
                x.ConstructionSiteId == constructionSiteId &&
                x.Status == PaymentStatus.Completed &&
                !x.IsDeleted)
            .SumAsync(
                x => x.Amount,
                cancellationToken);

        var pettyCash = await _dbContext.PettyCashAccounts
            .Where(x =>
                x.CompanyId == companyId &&
                x.ConstructionSiteId == constructionSiteId &&
                x.IsActive)
            .SumAsync(
                x => x.CurrentBalance,
                cancellationToken);

        // ============================================================
        // SITE RECEIVABLES
        // ============================================================

        var outstandingReceivables =
            await _dbContext.Invoices
                .Where(x =>
                    x.CompanyId == companyId &&
                    x.ConstructionSiteId == constructionSiteId &&
                    x.Type == InvoiceType.ClientInvoice &&
                    x.Status != InvoiceStatus.Draft &&
                    x.Status != InvoiceStatus.Cancelled &&
                    !x.IsDeleted)
                .SumAsync(
                    x => x.TotalAmount,
                    cancellationToken)
            -
            await _dbContext.Payments
                .Where(x =>
                    x.CompanyId == companyId &&
                    x.ConstructionSiteId == constructionSiteId &&
                    x.Direction == PaymentDirection.Incoming &&
                    x.Status == PaymentStatus.Completed &&
                    x.InvoiceId != null &&
                    !x.IsDeleted)
                .SumAsync(
                    x => x.Amount,
                    cancellationToken);

        // ============================================================
        // SITE PAYABLES
        // ============================================================

        var outstandingPayables =
            await _dbContext.Invoices
                .Where(x =>
                    x.CompanyId == companyId &&
                    x.ConstructionSiteId == constructionSiteId &&
                    x.Type == InvoiceType.VendorBill &&
                    x.Status != InvoiceStatus.Draft &&
                    x.Status != InvoiceStatus.Cancelled &&
                    !x.IsDeleted)
                .SumAsync(
                    x => x.TotalAmount,
                    cancellationToken)
            -
            await _dbContext.Payments
                .Where(x =>
                    x.CompanyId == companyId &&
                    x.ConstructionSiteId == constructionSiteId &&
                    x.Direction == PaymentDirection.Outgoing &&
                    x.Status == PaymentStatus.Completed &&
                    x.InvoiceId != null &&
                    !x.IsDeleted)
                .SumAsync(
                    x => x.Amount,
                    cancellationToken);

        // ============================================================
        // SITE NET CASH FLOW
        // ============================================================

        var incomingCash = await _dbContext.Payments
            .Where(x =>
                x.CompanyId == companyId &&
                x.ConstructionSiteId == constructionSiteId &&
                x.Direction == PaymentDirection.Incoming &&
                x.Status == PaymentStatus.Completed &&
                !x.IsDeleted)
            .SumAsync(
                x => x.Amount,
                cancellationToken);

        var outgoingCash = await _dbContext.Payments
            .Where(x =>
                x.CompanyId == companyId &&
                x.ConstructionSiteId == constructionSiteId &&
                x.Direction == PaymentDirection.Outgoing &&
                x.Status == PaymentStatus.Completed &&
                !x.IsDeleted)
            .SumAsync(
                x => x.Amount,
                cancellationToken);

        var netCashFlow = incomingCash - outgoingCash;

        return new SiteFinancialSummaryResponse
        {
            ConstructionSiteId = site.Id,
            ConstructionSiteName = site.Name,
            BudgetAmount = budget,
            ExpenseAmount = expenses,
            InvoiceAmount = invoices,
            PaymentAmount = payments,
            PettyCashBalance = pettyCash,
            OutstandingReceivables = outstandingReceivables,
            OutstandingPayables = outstandingPayables,
            NetCashFlow = netCashFlow
        };
    }
}