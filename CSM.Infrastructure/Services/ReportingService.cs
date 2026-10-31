using CSM.Application.Common.Exceptions;
using CSM.Application.Common.Security;
using CSM.Application.Reporting;
using CSM.Application.Reporting.Dtos;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Identity;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using CSM.Domain.Enums;

namespace CSM.Infrastructure.Services;

public sealed class ReportingService : IReportingService
{
    private readonly ApplicationDbContext _dbContext;

    public ReportingService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ExecutiveDashboardResponse> GetExecutiveDashboardAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default,
        Guid? selectedCompanyId = null)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureHasPermission(
            actor,
            GranularPermission.Reports_View);

        var companyId = await GetCompanyIdAsync(actor, selectedCompanyId, cancellationToken);
        var scope = new FinancialReadScope(_dbContext, actor, companyId);
        var canFinance = HasPermission(actor, GranularPermission.Reports_FinancialSummary_View);

        var sites = scope.Sites
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId);

        var projects = scope.Projects
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId);

        var budgets = scope.Finance(_dbContext.Budgets)
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId);

        var expenses = scope.Finance(_dbContext.Expenses)
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId);

        var invoices = scope.Finance(_dbContext.Invoices)
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId);

        var payments = scope.Finance(_dbContext.Payments)
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId);

        var totalSites = await sites.CountAsync(
            cancellationToken);

        var activeSites = await sites.CountAsync(
            x => x.IsActive,
            cancellationToken);

        var totalProjects = await projects.CountAsync(
            cancellationToken);

        decimal? totalContractValue = canFinance ? (await projects
            .SumAsync(
                x => (decimal?)x.ContractValue,
                cancellationToken) ?? 0m) : null;

        decimal? totalBudget = canFinance ? (await budgets
            .SumAsync(
                x => (decimal?)x.TotalAmount,
                cancellationToken) ?? 0m) : null;

        decimal? totalExpenses = canFinance ? (await expenses
            .Where(x => x.Status != Domain.Enums.ExpenseStatus.Cancelled)
            .SumAsync(
                x => (decimal?)x.Amount + x.TaxAmount,
                cancellationToken) ?? 0m) : null;

        decimal? totalInvoices = canFinance ? (await invoices
            .Where(x => x.Status != Domain.Enums.InvoiceStatus.Cancelled)
            .SumAsync(
                x => (decimal?)x.TotalAmount,
                cancellationToken) ?? 0m) : null;

        decimal? totalCompletedPayments = canFinance ? (await payments
            .Where(x => x.Status == Domain.Enums.PaymentStatus.Completed)
            .SumAsync(
                x => (decimal?)x.Amount,
                cancellationToken) ?? 0m) : null;

        decimal? outstandingInvoices = canFinance
            ? Math.Max(0m, totalInvoices!.Value - totalCompletedPayments!.Value) : null;

        var sitesByStatus = await sites
            .GroupBy(x => x.Status)
            .Select(
                x => new
                {
                    Status = x.Key,
                    Count = x.Count()
                })
            .ToDictionaryAsync(
                x => x.Status,
                x => x.Count,
                cancellationToken);

        var projectsByStatus = await projects
            .GroupBy(x => x.Status)
            .Select(
                x => new
                {
                    Status = x.Key,
                    Count = x.Count()
                })
            .ToDictionaryAsync(
                x => x.Status,
                x => x.Count,
                cancellationToken);

        return new ExecutiveDashboardResponse(
            totalSites,
            activeSites,
            totalProjects,
            totalContractValue,
            totalBudget,
            totalExpenses,
            totalInvoices,
            totalCompletedPayments,
            outstandingInvoices,
            sitesByStatus,
            projectsByStatus);
    }

    public async Task<FinancialSummaryResponse> GetFinancialSummaryAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default,
        Guid? selectedCompanyId = null)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureHasPermission(
            actor,
            GranularPermission.Reports_FinancialSummary_View);

        var companyId = await GetCompanyIdAsync(actor, selectedCompanyId, cancellationToken);
        var scope = new FinancialReadScope(_dbContext, actor, companyId);
        if (!scope.IncludesCompany) throw new UnauthorizedAccessException("Company-level financial reports are not available in your scope.");

        var totalBudget = await scope.Finance(_dbContext.Budgets)
            .AsNoTracking()
            .Where(
                x =>
                    x.CompanyId == companyId &&
                    !x.IsDeleted &&
                    x.ConstructionSiteId == null)
            .SumAsync(
                x => (decimal?)x.TotalAmount,
                cancellationToken) ?? 0m;

        var totalExpenses = await scope.Finance(_dbContext.Expenses)
            .AsNoTracking()
            .Where(
                x =>
                    x.CompanyId == companyId &&
                    x.ConstructionSiteId == null &&
                    x.Status != Domain.Enums.ExpenseStatus.Cancelled)
            .SumAsync(
                x => (decimal?)x.Amount + x.TaxAmount,
                cancellationToken) ?? 0m;

        var invoiceData = await scope.Finance(_dbContext.Invoices)
            .AsNoTracking()
            .Where(
                x =>
                    x.CompanyId == companyId &&
                    x.ConstructionSiteId == null &&
                    x.Status != Domain.Enums.InvoiceStatus.Cancelled)
            .GroupBy(x => 1)
            .Select(
                x => new
                {
                    TotalInvoices =
                        x.Sum(i => i.TotalAmount),

                    ClientInvoices =
                        x.Where(
                            i =>
                                i.Type ==
                                Domain.Enums.InvoiceType.ClientInvoice)
                            .Sum(i => i.TotalAmount),

                    VendorBills =
                        x.Where(
                            i =>
                                i.Type ==
                                Domain.Enums.InvoiceType.VendorBill)
                            .Sum(i => i.TotalAmount)
                })
            .FirstOrDefaultAsync(cancellationToken);

        var totalInvoices =
            invoiceData?.TotalInvoices ?? 0m;

        var clientInvoices =
            invoiceData?.ClientInvoices ?? 0m;

        var vendorBills =
            invoiceData?.VendorBills ?? 0m;

        var completedIncomingPayments =
            await scope.Finance(_dbContext.Payments)
                .AsNoTracking()
                .Where(
                    x =>
                        x.CompanyId == companyId &&
                        x.ConstructionSiteId == null &&
                        x.Status ==
                            Domain.Enums.PaymentStatus.Completed &&
                        x.Direction ==
                            Domain.Enums.PaymentDirection.Incoming)
                .SumAsync(
                    x => (decimal?)x.Amount,
                    cancellationToken) ?? 0m;

        var completedOutgoingPayments =
            await scope.Finance(_dbContext.Payments)
                .AsNoTracking()
                .Where(
                    x =>
                        x.CompanyId == companyId &&
                        x.ConstructionSiteId == null &&
                        x.Status ==
                            Domain.Enums.PaymentStatus.Completed &&
                        x.Direction ==
                            Domain.Enums.PaymentDirection.Outgoing)
                .SumAsync(
                    x => (decimal?)x.Amount,
                    cancellationToken) ?? 0m;


        var pettyCashCashIn =
            await scope.Transactions
                .AsNoTracking()
                .Where(
                    x =>
                        x.PettyCashAccount.CompanyId == companyId &&
                        x.PettyCashAccount.ConstructionSiteId == null &&
                        x.TransactionType == "CashIn")
                .SumAsync(
                    x => (decimal?)x.Amount,
                    cancellationToken) ?? 0m;

        var pettyCashCashOut =
            await scope.Transactions
                .AsNoTracking()
                .Where(
                    x =>
                        x.PettyCashAccount.CompanyId == companyId &&
                        x.PettyCashAccount.ConstructionSiteId == null &&
                        x.TransactionType == "CashOut")
                .SumAsync(
                    x => (decimal?)x.Amount,
                    cancellationToken) ?? 0m;

        var pettyCashNetMovement =
            pettyCashCashIn - pettyCashCashOut;

        var totalCashIn =
            completedIncomingPayments + pettyCashCashIn;

        var totalExpenditure =
            completedOutgoingPayments + pettyCashCashOut;

        var balance =
            totalBudget +
            totalCashIn -
            totalExpenditure;

        var netCashFlow =
            completedIncomingPayments -
            completedOutgoingPayments +
            pettyCashNetMovement;

        var pettyCashBalance =
            await scope.Finance(_dbContext.PettyCashAccounts)
                .AsNoTracking()
                .Where(
                    x =>
                        x.CompanyId == companyId &&
                        x.ConstructionSiteId == null &&
                        x.IsActive)
                .SumAsync(
                    x => (decimal?)x.CurrentBalance,
                    cancellationToken) ?? 0m;

        var activePettyCashAccounts =
            await scope.Finance(_dbContext.PettyCashAccounts)
                .AsNoTracking()
                .CountAsync(
                    x =>
                        x.CompanyId == companyId &&
                        x.ConstructionSiteId == null &&
                        x.IsActive,
                    cancellationToken);

        var companyPettyCashBalance =
            await scope.Finance(_dbContext.PettyCashAccounts)
                .AsNoTracking()
                .Where(
                    x =>
                        x.CompanyId == companyId &&
                        x.ConstructionSiteId == null &&
                        x.IsActive)
                .SumAsync(
                    x => (decimal?)x.CurrentBalance,
                    cancellationToken) ?? 0m;

        var companyPettyCashAccounts =
            await scope.Finance(_dbContext.PettyCashAccounts)
                .AsNoTracking()
                .CountAsync(
                    x =>
                        x.CompanyId == companyId &&
                        x.ConstructionSiteId == null &&
                        x.IsActive,
                    cancellationToken);

        var budgetUtilization =
            totalBudget == 0
                ? 0m
                : Math.Round((totalExpenses / totalBudget) * 100m, 2);

        var outstandingReceivables =
            Math.Max(
                0m,
                clientInvoices - completedIncomingPayments);

        var outstandingPayables =
            Math.Max(
                0m,
                vendorBills - completedOutgoingPayments);

        return new FinancialSummaryResponse(
            totalBudget,
            totalExpenses,
            totalExpenditure,
            balance,

            totalInvoices,
            totalCashIn,
            outstandingReceivables,
            outstandingPayables,

            netCashFlow,

            pettyCashBalance,
            pettyCashCashOut,
            activePettyCashAccounts,

            budgetUtilization,

            companyPettyCashBalance,
            companyPettyCashAccounts);
    }

    public async Task<FinancialSummaryResponse>
        GetSiteFinancialSummaryAsync(
            Guid currentUserId,
            Guid constructionSiteId,
            CancellationToken cancellationToken = default,
        Guid? selectedCompanyId = null)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureHasPermission(
            actor,
            GranularPermission.Reports_FinancialSummary_View);

        var companyId = await GetCompanyIdAsync(actor, selectedCompanyId, cancellationToken);
        var scope = new FinancialReadScope(_dbContext, actor, companyId);

        var site = await scope.Sites
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x =>
                    x.Id == constructionSiteId &&
                    x.CompanyId == companyId &&
                    !x.IsDeleted,
                cancellationToken)
            ?? throw new KeyNotFoundException("Construction site was not found.");

        var totalBudget = await scope.Finance(_dbContext.Budgets)
            .AsNoTracking()
            .Where(
                x =>
                    x.CompanyId == companyId &&
                    x.ConstructionSiteId == site.Id &&
                    !x.IsDeleted)
            .SumAsync(
                x => (decimal?)x.TotalAmount,
                cancellationToken) ?? 0m;

        var totalExpenses = await scope.Finance(_dbContext.Expenses)
            .AsNoTracking()
            .Where(
                x =>
                    x.CompanyId == companyId &&
                    x.ConstructionSiteId == site.Id &&
                    !x.IsDeleted &&
                    x.Status != Domain.Enums.ExpenseStatus.Cancelled)
            .SumAsync(
                x => (decimal?)x.Amount + x.TaxAmount,
                cancellationToken) ?? 0m;

        var invoiceData = await scope.Finance(_dbContext.Invoices)
            .AsNoTracking()
            .Where(
                x =>
                    x.CompanyId == companyId &&
                    x.ConstructionSiteId == site.Id &&
                    !x.IsDeleted &&
                    x.Status != Domain.Enums.InvoiceStatus.Cancelled)
            .GroupBy(x => 1)
            .Select(
                x => new
                {
                    TotalInvoices =
                        x.Sum(i => i.TotalAmount),

                    ClientInvoices =
                        x.Where(
                            i =>
                                i.Type ==
                                Domain.Enums.InvoiceType.ClientInvoice)
                        .Sum(i => i.TotalAmount),

                    VendorBills =
                        x.Where(
                            i =>
                                i.Type ==
                                Domain.Enums.InvoiceType.VendorBill)
                        .Sum(i => i.TotalAmount)
                })
            .FirstOrDefaultAsync(cancellationToken);

        var totalInvoices =
            invoiceData?.TotalInvoices ?? 0m;

        var clientInvoices =
            invoiceData?.ClientInvoices ?? 0m;

        var vendorBills =
            invoiceData?.VendorBills ?? 0m;

        var completedIncomingPayments =
            await scope.Finance(_dbContext.Payments)
                .AsNoTracking()
                .Where(
                    x =>
                        x.CompanyId == companyId &&
                        x.ConstructionSiteId == site.Id &&
                        x.Status ==
                        Domain.Enums.PaymentStatus.Completed &&
                        x.Direction ==
                        Domain.Enums.PaymentDirection.Incoming)
                .SumAsync(
                    x => (decimal?)x.Amount,
                    cancellationToken) ?? 0m;

        var completedOutgoingPayments =
            await scope.Finance(_dbContext.Payments)
                .AsNoTracking()
                .Where(
                    x =>
                        x.CompanyId == companyId &&
                        x.ConstructionSiteId == site.Id &&
                        x.Status ==
                        Domain.Enums.PaymentStatus.Completed &&
                        x.Direction ==
                        Domain.Enums.PaymentDirection.Outgoing)
                .SumAsync(
                    x => (decimal?)x.Amount,
                    cancellationToken) ?? 0m;



        var outstandingReceivables =
            Math.Max(
                0m,
                clientInvoices - completedIncomingPayments);

        var outstandingPayables =
            Math.Max(
                0m,
                vendorBills - completedOutgoingPayments);

        var remainingBudget =
            Math.Max(
                0m,
                totalBudget - totalExpenses);

        var pettyCashCashIn =
            await scope.Transactions
                .AsNoTracking()
                .Where(
                    x =>
                        x.PettyCashAccount.CompanyId == companyId &&
                        x.PettyCashAccount.ConstructionSiteId == site.Id &&
                        x.TransactionType == "CashIn")
                .SumAsync(
                    x => (decimal?)x.Amount,
                    cancellationToken) ?? 0m;

        var pettyCashCashOut =
            await scope.Transactions
                .AsNoTracking()
                .Where(
                    x =>
                        x.PettyCashAccount.CompanyId == companyId &&
                        x.PettyCashAccount.ConstructionSiteId == site.Id &&
                        x.TransactionType == "CashOut")
                .SumAsync(
                    x => (decimal?)x.Amount,
                    cancellationToken) ?? 0m;

        var pettyCashNetMovement =
            pettyCashCashIn - pettyCashCashOut;

        var totalCashIn =
            completedIncomingPayments + pettyCashCashIn;

        var totalExpenditure =
            completedOutgoingPayments + pettyCashCashOut;

        var balance =
            totalBudget +
            totalCashIn -
            totalExpenditure;

        var netCashFlow =
            completedIncomingPayments -
            completedOutgoingPayments +
            pettyCashNetMovement;

        var budgetUtilizationPercent =
            totalBudget > 0m
                ? (totalExpenses / totalBudget) * 100m
                : 0m;

        var pettyCashData = await scope.Finance(_dbContext.PettyCashAccounts)
            .AsNoTracking()
            .Where(
                x =>
                    x.CompanyId == companyId &&
                    x.ConstructionSiteId == site.Id)
            .GroupBy(x => 1)
            .Select(
                x => new
                {
                    Balance = x.Sum(a => a.CurrentBalance),
                    ActiveAccounts = x.Count(a => a.IsActive)
                })
            .FirstOrDefaultAsync(cancellationToken);

        var pettyCashBalance =
            pettyCashData?.Balance ?? 0m;

        var activePettyCashAccounts =
            pettyCashData?.ActiveAccounts ?? 0;

        return new FinancialSummaryResponse(
            totalBudget,
            totalExpenses,
            totalExpenditure,
            balance,

            totalInvoices,
            totalCashIn,
            outstandingReceivables,
            outstandingPayables,

            netCashFlow,

            pettyCashBalance,
            pettyCashCashOut,
            activePettyCashAccounts,

            budgetUtilizationPercent,

            0m,
            0);
    }

    public async Task<AllFinancialSummaryResponse>
        GetAllFinancialSummariesAsync(
            Guid currentUserId,
            CancellationToken cancellationToken = default,
        Guid? selectedCompanyId = null)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureHasPermission(
            actor,
            GranularPermission.Reports_FinancialSummary_View);

        var companyId = await GetCompanyIdAsync(actor, selectedCompanyId, cancellationToken);
        var scope = new FinancialReadScope(_dbContext, actor, companyId);

        // Preserve the existing Company Financial Report.
        FinancialSummaryResponse? companySummary = scope.IncludesCompany
            ? await GetFinancialSummaryAsync(currentUserId, cancellationToken, selectedCompanyId)
            : null;

        // Get all active/non-deleted sites belonging to the company.
        var sites = await scope.Sites
            .AsNoTracking()
            .Where(
                x =>
                    x.CompanyId == companyId &&
                    !x.IsDeleted)
            .OrderBy(x => x.Name)
            .Select(
                x => new
                {
                    x.Id,
                    x.Name,
                    x.SiteCode
                })
            .ToListAsync(cancellationToken);

        var siteIds = sites
            .Select(x => x.Id)
            .ToHashSet();

        // Preserve the existing per-site financial reports.
        var siteSummaries =
            new List<SiteFinancialSummaryResponse>();

        foreach (var site in sites)
        {
            var financial =
                await GetSiteFinancialSummaryAsync(
                    currentUserId,
                    site.Id,
                    cancellationToken,
                    selectedCompanyId);

            siteSummaries.Add(
                new SiteFinancialSummaryResponse(
                    site.Id,
                    site.Name,
                    site.SiteCode,
                    financial));
        }

        // ============================================================
        // ALL FINANCIAL SUMMARY TOTAL
        //
        // This is intentionally calculated directly from the database.
        // It is NOT calculated by adding Company + Site response objects.
        // ============================================================

        // ------------------------------------------------------------
        // Budget
        // ------------------------------------------------------------
        var totalBudget = await scope.Finance(_dbContext.Budgets)
            .AsNoTracking()
            .Where(
                x =>
                    x.CompanyId == companyId &&
                    !x.IsDeleted &&
                    (
                        x.ConstructionSiteId == null ||
                        siteIds.Contains(x.ConstructionSiteId.Value)
                    ))
            .SumAsync(
                x => (decimal?)x.TotalAmount,
                cancellationToken) ?? 0m;

        // ------------------------------------------------------------
        // Expenses
        // ------------------------------------------------------------
        var totalExpenses = await scope.Finance(_dbContext.Expenses)
            .AsNoTracking()
            .Where(
                x =>
                    x.CompanyId == companyId &&
                    !x.IsDeleted &&
                    x.Status != Domain.Enums.ExpenseStatus.Cancelled &&
                    (
                        x.ConstructionSiteId == null ||
                        siteIds.Contains(x.ConstructionSiteId.Value)
                    ))
            .SumAsync(
                x => (decimal?)x.Amount + x.TaxAmount,
                cancellationToken) ?? 0m;

        // ------------------------------------------------------------
        // Invoices
        // ------------------------------------------------------------
        var invoiceData = await scope.Finance(_dbContext.Invoices)
            .AsNoTracking()
            .Where(
                x =>
                    x.CompanyId == companyId &&
                    !x.IsDeleted &&
                    x.Status != Domain.Enums.InvoiceStatus.Cancelled &&
                    (
                        x.ConstructionSiteId == null ||
                        siteIds.Contains(x.ConstructionSiteId.Value)
                    ))
            .GroupBy(x => 1)
            .Select(
                x => new
                {
                    TotalInvoices =
                        x.Sum(i => i.TotalAmount),

                    ClientInvoices =
                        x.Where(
                            i =>
                                i.Type ==
                                Domain.Enums.InvoiceType.ClientInvoice)
                        .Sum(i => i.TotalAmount),

                    VendorBills =
                        x.Where(
                            i =>
                                i.Type ==
                                Domain.Enums.InvoiceType.VendorBill)
                        .Sum(i => i.TotalAmount)
                })
            .FirstOrDefaultAsync(cancellationToken);

        var totalInvoices =
            invoiceData?.TotalInvoices ?? 0m;

        var clientInvoices =
            invoiceData?.ClientInvoices ?? 0m;

        var vendorBills =
            invoiceData?.VendorBills ?? 0m;

        // ------------------------------------------------------------
        // Incoming payments
        // ------------------------------------------------------------
        var completedIncomingPayments =
            await scope.Finance(_dbContext.Payments)
                .AsNoTracking()
                .Where(
                    x =>
                        x.CompanyId == companyId &&
                        x.Status ==
                            Domain.Enums.PaymentStatus.Completed &&
                        x.Direction ==
                            Domain.Enums.PaymentDirection.Incoming &&
                        (
                            x.ConstructionSiteId == null ||
                            siteIds.Contains(x.ConstructionSiteId.Value)
                        ))
                .SumAsync(
                    x => (decimal?)x.Amount,
                    cancellationToken) ?? 0m;

        // ------------------------------------------------------------
        // Outgoing payments
        // ------------------------------------------------------------
        var completedOutgoingPayments =
            await scope.Finance(_dbContext.Payments)
                .AsNoTracking()
                .Where(
                    x =>
                        x.CompanyId == companyId &&
                        x.Status ==
                            Domain.Enums.PaymentStatus.Completed &&
                        x.Direction ==
                            Domain.Enums.PaymentDirection.Outgoing &&
                        (
                            x.ConstructionSiteId == null ||
                            siteIds.Contains(x.ConstructionSiteId.Value)
                        ))
                .SumAsync(
                    x => (decimal?)x.Amount,
                    cancellationToken) ?? 0m;



        // ------------------------------------------------------------
        // Petty cash movement
        // ------------------------------------------------------------
        var pettyCashCashIn =
            await scope.Transactions
                .AsNoTracking()
                .Where(
                    x =>
                        x.PettyCashAccount.CompanyId == companyId &&
                        (
                            x.PettyCashAccount.ConstructionSiteId == null ||
                            siteIds.Contains(
                                x.PettyCashAccount.ConstructionSiteId.Value)
                        ) &&
                        x.TransactionType == "CashIn")
                .SumAsync(
                    x => (decimal?)x.Amount,
                    cancellationToken) ?? 0m;

        var pettyCashCashOut =
            await scope.Transactions
                .AsNoTracking()
                .Where(
                    x =>
                        x.PettyCashAccount.CompanyId == companyId &&
                        (
                            x.PettyCashAccount.ConstructionSiteId == null ||
                            siteIds.Contains(
                                x.PettyCashAccount.ConstructionSiteId.Value)
                        ) &&
                        x.TransactionType == "CashOut")
                .SumAsync(
                    x => (decimal?)x.Amount,
                    cancellationToken) ?? 0m;

        var pettyCashNetMovement =
            pettyCashCashIn - pettyCashCashOut;

        var totalCashIn =
            completedIncomingPayments + pettyCashCashIn;

        var totalExpenditure =
            completedOutgoingPayments + pettyCashCashOut;

        var balance =
            totalBudget +
            totalCashIn -
            totalExpenditure;

        // ------------------------------------------------------------
        // Net cash flow
        // ------------------------------------------------------------
        var netCashFlow =
            completedIncomingPayments -
            completedOutgoingPayments +
            pettyCashNetMovement;

        // ------------------------------------------------------------
        // Outstanding receivables/payables
        // ------------------------------------------------------------
        var outstandingReceivables =
            Math.Max(
                0m,
                clientInvoices - completedIncomingPayments);

        var outstandingPayables =
            Math.Max(
                0m,
                vendorBills - completedOutgoingPayments);

        // ------------------------------------------------------------
        // Budget utilization
        // ------------------------------------------------------------
        var budgetUtilizationPercent =
            totalBudget == 0m
                ? 0m
                : Math.Round(
                    (totalExpenses / totalBudget) * 100m,
                    2);

        // ------------------------------------------------------------
        // Petty cash accounts
        // ------------------------------------------------------------
        var pettyCashData =
            await scope.Finance(_dbContext.PettyCashAccounts)
                .AsNoTracking()
                .Where(
                    x =>
                        x.CompanyId == companyId &&
                        (
                            x.ConstructionSiteId == null ||
                            siteIds.Contains(
                                x.ConstructionSiteId.Value)
                        ))
                .GroupBy(x => 1)
                .Select(
                    x => new
                    {
                        Balance =
                            x.Sum(a => a.CurrentBalance),

                        ActiveAccounts =
                            x.Count(a => a.IsActive)
                    })
                .FirstOrDefaultAsync(cancellationToken);

        var pettyCashBalance =
            pettyCashData?.Balance ?? 0m;

        var activePettyCashAccounts =
            pettyCashData?.ActiveAccounts ?? 0;

        // For the aggregate report, both compatibility fields represent
        // the complete company-wide petty cash balance/account count.
        var companyPettyCashBalance =
            pettyCashBalance;

        var companyPettyCashAccounts =
            activePettyCashAccounts;

        var totalSummary =
            new FinancialSummaryResponse(
                totalBudget,
                totalExpenses,
                totalExpenditure,
                balance,
                totalInvoices,
                totalCashIn,
                outstandingReceivables,
                outstandingPayables,
                netCashFlow,
                pettyCashBalance,
                pettyCashCashOut,
                activePettyCashAccounts,
                budgetUtilizationPercent,
                companyPettyCashBalance,
                companyPettyCashAccounts);

        return new AllFinancialSummaryResponse(
            companySummary,
            siteSummaries,
            totalSummary);
        }

    private async Task<User> GetActorAsync(
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Users
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .ThenInclude(x => x.GranularPermissions)
            .SingleOrDefaultAsync(
                x => x.Id == currentUserId && x.IsActive && !x.IsDeleted,
                cancellationToken)
            ?? throw new ReportingManagementException(
                "Authenticated user was not found.");
    }

    public async Task<ExecutiveDashboardResponse>
        GetSiteExecutiveDashboardAsync(
            Guid currentUserId,
            Guid constructionSiteId,
            CancellationToken cancellationToken = default,
        Guid? selectedCompanyId = null)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureHasPermission(
            actor,
            GranularPermission.Reports_View);

        var companyId = await GetCompanyIdAsync(actor, selectedCompanyId, cancellationToken);
        var scope = new FinancialReadScope(_dbContext, actor, companyId);
        var canFinance = HasPermission(actor, GranularPermission.Reports_FinancialSummary_View);

        var site = await scope.Sites
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x =>
                    x.Id == constructionSiteId &&
                    x.CompanyId == companyId &&
                    !x.IsDeleted,
                cancellationToken)
            ?? throw new KeyNotFoundException("Construction site was not found.");

        var projects = await scope.Projects
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == companyId &&
                x.ConstructionSiteId == constructionSiteId &&
                !x.IsDeleted)
            .ToListAsync(cancellationToken);

        var budgets = canFinance ? await scope.Finance(_dbContext.Budgets)
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == companyId &&
                x.ConstructionSiteId == constructionSiteId &&
                !x.IsDeleted)
            .ToListAsync(cancellationToken) : new List<CSM.Domain.Entities.Finance.Budget>();

        var expenses = canFinance ? await scope.Finance(_dbContext.Expenses)
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == companyId &&
                x.ConstructionSiteId == constructionSiteId &&
                !x.IsDeleted)
            .ToListAsync(cancellationToken) : new List<CSM.Domain.Entities.Finance.Expense>();

        var invoices = canFinance ? await scope.Finance(_dbContext.Invoices)
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == companyId &&
                x.ConstructionSiteId == constructionSiteId &&
                !x.IsDeleted)
            .ToListAsync(cancellationToken) : new List<CSM.Domain.Entities.Finance.Invoice>();

        var payments = canFinance ? await scope.Finance(_dbContext.Payments)
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == companyId &&
                x.Invoice != null &&
                x.Invoice.ConstructionSiteId == constructionSiteId &&
                !x.IsDeleted)
            .ToListAsync(cancellationToken) : new List<CSM.Domain.Entities.Finance.Payment>();

        var totalBudget = budgets.Sum(x => x.TotalAmount);

        var totalExpenses = expenses
            .Where(x => x.Status != CSM.Domain.Enums.ExpenseStatus.Cancelled)
            .Sum(x => x.Amount + x.TaxAmount);

        var totalInvoices = invoices
            .Where(x => x.Status != CSM.Domain.Enums.InvoiceStatus.Cancelled)
            .Sum(x => x.TotalAmount);

        var totalCompletedPayments = payments
            .Where(x =>
                x.Status == CSM.Domain.Enums.PaymentStatus.Completed)
            .Sum(x => x.Amount);

        var outstandingInvoices =
            Math.Max(
                0,
                totalInvoices - totalCompletedPayments);

        return new ExecutiveDashboardResponse(
            1,
            site.IsActive ? 1 : 0,
            projects.Count,
            canFinance ? projects.Sum(x => x.ContractValue) : null,
            canFinance ? totalBudget : null,
            canFinance ? totalExpenses : null,
            canFinance ? totalInvoices : null,
            canFinance ? totalCompletedPayments : null,
            canFinance ? outstandingInvoices : null,
            new Dictionary<CSM.Domain.Enums.SiteStatus, int>
            {
                [site.Status] = 1
            },
            projects
                .GroupBy(x => x.Status)
                .ToDictionary(
                    x => x.Key,
                    x => x.Count()));
    }

   private static void EnsureHasPermission(
       User actor,
       GranularPermission permission)
   {
       if (HasPermission(actor, permission))
       {
           return;
       }

       throw new UnauthorizedAccessException("You are not authorized to view this report.");
   }

   private static bool HasPermission(
       User actor,
       GranularPermission permission)
   {
       return actor.UserRoles.Any(
           userRole =>
               !userRole.IsDeleted &&
               !userRole.Role.IsDeleted &&
               userRole.Role.GranularPermissions.Any(
                   rolePermission =>
                       rolePermission.Permission == permission));
   }

    private async Task<Guid> GetCompanyIdAsync(
        User actor, Guid? selectedCompanyId, CancellationToken cancellationToken)
    {
        var isSuperAdmin = HasRole(actor, AppRoles.SuperAdmin);
        if (!isSuperAdmin && selectedCompanyId.HasValue && selectedCompanyId != actor.CompanyId)
        {
            throw new ReportingManagementException("You are not authorized to view this company's reports.");
        }

        var companyId = isSuperAdmin ? selectedCompanyId ?? actor.CompanyId : actor.CompanyId;
        if (!companyId.HasValue || companyId == Guid.Empty)
        {
            throw new ReportingManagementException("Select a company to view its reports.");
        }

        if (!await _dbContext.Companies.AsNoTracking().AnyAsync(
                x => x.Id == companyId.Value && !x.IsDeleted, cancellationToken))
        {
            throw new ReportingManagementException("The selected company is not available.");
        }

        return companyId.Value;
    }

    private static bool HasRole(
        User actor,
        string roleName)
    {
        return actor.UserRoles.Any(
            x =>
                !x.IsDeleted &&
                !x.Role.IsDeleted &&
                string.Equals(
                    x.Role.Name,
                    roleName,
                    StringComparison.OrdinalIgnoreCase));
    }
}
