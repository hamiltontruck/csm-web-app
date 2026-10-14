using CSM.Web.Models.Reporting;

namespace CSM.Web.Services;

public interface IReportingService
{
    Task<ExecutiveDashboardResponse> GetExecutiveDashboardAsync();

    Task<FinancialSummaryResponse> GetFinancialSummaryAsync();

    Task<AllFinancialSummaryResponse>
        GetAllFinancialSummariesAsync();

    Task<ExecutiveDashboardResponse>
        GetSiteExecutiveDashboardAsync(Guid constructionSiteId);

    Task<FinancialSummaryResponse>
        GetSiteFinancialSummaryAsync(Guid constructionSiteId);
}