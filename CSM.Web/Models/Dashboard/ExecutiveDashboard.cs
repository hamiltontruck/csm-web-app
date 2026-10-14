namespace CSM.Web.Models.Dashboard;

public class ExecutiveDashboard
{
    public int ActiveProjects { get; set; }
    public int CompletedProjects { get; set; }

    public decimal TotalBudget { get; set; }
    public decimal TotalExpenses { get; set; }

    public decimal BudgetUtilization { get; set; }

    public int OpenDelays { get; set; }

    public int TotalEmployees { get; set; }

    public int LowStockItems { get; set; }

    public int OpenSafetyIncidents { get; set; }

    public DateTime GeneratedAtUtc { get; set; }
}