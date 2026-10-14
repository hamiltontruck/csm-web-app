namespace CSM.Web.Models.Reporting;

public sealed class FinancialSummaryResponse
{
    public decimal TotalBudget { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal TotalExpenditure { get; set; }
    public decimal Balance { get; set; }

    public decimal TotalInvoices { get; set; }
    public decimal TotalCompletedPayments { get; set; }
    public decimal OutstandingReceivables { get; set; }
    public decimal OutstandingPayables { get; set; }

    public decimal NetCashFlow { get; set; }

    public decimal PettyCashBalance { get; set; }
    public decimal TotalPettyCashOut { get; set; }
    public int ActivePettyCashAccounts { get; set; }

    public decimal BudgetUtilizationPercent { get; set; }

    public decimal CompanyPettyCashBalance { get; set; }
    public int CompanyPettyCashAccounts { get; set; }
}