using CSM.Web.Models.Dashboard;

namespace CSM.Web.Services;

public interface IExecutiveDashboardService
{
    Task<ExecutiveDashboard?> GetDashboardAsync();
}