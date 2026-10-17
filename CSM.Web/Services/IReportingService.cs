using CSM.Web.Models.Reporting;

namespace CSM.Web.Services;

public interface IReportingService
{
    Task<ExecutiveDashboardResponse> GetExecutiveDashboardAsync(Guid? selectedCompanyId = null);

    Task<FinancialSummaryResponse> GetFinancialSummaryAsync(Guid? selectedCompanyId = null);

    Task<AllFinancialSummaryResponse>
        GetAllFinancialSummariesAsync(Guid? selectedCompanyId = null);

    Task<ExecutiveDashboardResponse>
        GetSiteExecutiveDashboardAsync(Guid constructionSiteId, Guid? selectedCompanyId = null);

    Task<FinancialSummaryResponse>
        GetSiteFinancialSummaryAsync(Guid constructionSiteId, Guid? selectedCompanyId = null);
}