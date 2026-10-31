namespace CSM.Web.Models.Reporting;

public sealed class ExecutiveDashboardResponse
{
    public int TotalSites { get; set; }
    public int ActiveSites { get; set; }
    public int TotalProjects { get; set; }
    public decimal? TotalContractValue { get; set; }
    public decimal? TotalBudget { get; set; }
    public decimal? TotalExpenses { get; set; }
    public decimal? TotalInvoices { get; set; }
    public decimal? TotalCompletedPayments { get; set; }
    public decimal? OutstandingInvoices { get; set; }

    public Dictionary<string, int> SitesByStatus { get; set; } = new();

    public Dictionary<string, int> ProjectsByStatus { get; set; } = new();
}