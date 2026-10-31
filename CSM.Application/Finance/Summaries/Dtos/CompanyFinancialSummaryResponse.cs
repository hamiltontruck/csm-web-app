namespace CSM.Application.Finance.Summaries.Dtos;

public sealed class CompanyFinancialSummaryResponse
{
    public bool IncludesCompanyLevel { get; init; } = true;

    public decimal TotalBudget { get; init; }

    public decimal TotalExpenses { get; init; }

    public decimal TotalInvoiceAmount { get; init; }

    public decimal TotalPayments { get; init; }

    public decimal TotalPettyCashBalance { get; init; }

    public decimal OutstandingReceivables { get; init; }

    public decimal OutstandingPayables { get; init; }

    public decimal NetCashFlow { get; init; }

    public IReadOnlyCollection<SiteFinancialSummaryResponse> Sites { get; init; }
        = Array.Empty<SiteFinancialSummaryResponse>();
}

public sealed class SiteFinancialSummaryResponse
{
    public Guid ConstructionSiteId { get; init; }

    public string ConstructionSiteName { get; init; }
        = string.Empty;

    public decimal BudgetAmount { get; init; }

    public decimal ExpenseAmount { get; init; }

    public decimal InvoiceAmount { get; init; }

    public decimal PaymentAmount { get; init; }

    public decimal PettyCashBalance { get; init; }

    public decimal OutstandingReceivables { get; init; }

    public decimal OutstandingPayables { get; init; }

    public decimal NetCashFlow { get; init; }

    public decimal BudgetVariance =>
        BudgetAmount - ExpenseAmount;
}