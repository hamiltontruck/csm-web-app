namespace CSM.Application.Reporting.Dtos;

public sealed record FinancialSummaryResponse(
    decimal TotalBudget,
    decimal TotalExpenses,
    decimal TotalExpenditure,
    decimal Balance,

    decimal TotalInvoices,
    decimal TotalCompletedPayments,
    decimal OutstandingReceivables,
    decimal OutstandingPayables,

    decimal NetCashFlow,

    decimal PettyCashBalance,
    decimal TotalPettyCashOut,
    int ActivePettyCashAccounts,

    decimal BudgetUtilizationPercent,

    decimal CompanyPettyCashBalance,
    int CompanyPettyCashAccounts
);