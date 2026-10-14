namespace CSM.Web.Models.Finance;

public sealed class CompanyFinancialSummaryResponse
{
    public decimal TotalBudget { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal TotalInvoiceAmount { get; set; }
    public decimal TotalPayments { get; set; }
    public decimal TotalPettyCashBalance { get; set; }
    public decimal OutstandingReceivables { get; init; }
    public decimal OutstandingPayables { get; init; }
    public decimal NetCashFlow { get; init; }

    public List<SiteFinancialSummaryResponse> Sites { get; set; } = [];
}

public sealed class SiteFinancialSummaryResponse
{
    public Guid ConstructionSiteId { get; set; }
    public string ConstructionSiteName { get; set; } = string.Empty;
    public decimal BudgetAmount { get; set; }
    public decimal ExpenseAmount { get; set; }
    public decimal InvoiceAmount { get; set; }
    public decimal PaymentAmount { get; set; }
    public decimal PettyCashBalance { get; set; }
    public decimal OutstandingReceivables { get; init; }
    public decimal OutstandingPayables { get; init; }
    public decimal NetCashFlow { get; init; }

    public decimal BudgetVariance =>
        BudgetAmount - ExpenseAmount;
}