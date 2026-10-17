using CSM.Application.Reporting.Dtos;

namespace CSM.Application.Reporting;

public interface IReportingService
{
    Task<ExecutiveDashboardResponse> GetExecutiveDashboardAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default,
        Guid? selectedCompanyId = null);

    Task<FinancialSummaryResponse> GetFinancialSummaryAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default,
        Guid? selectedCompanyId = null);

    Task<ExecutiveDashboardResponse> GetSiteExecutiveDashboardAsync(
        Guid currentUserId,
        Guid constructionSiteId,
        CancellationToken cancellationToken = default,
        Guid? selectedCompanyId = null);

    Task<FinancialSummaryResponse> GetSiteFinancialSummaryAsync(
        Guid currentUserId,
        Guid constructionSiteId,
        CancellationToken cancellationToken = default,
        Guid? selectedCompanyId = null);

    Task<AllFinancialSummaryResponse> GetAllFinancialSummariesAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default,
        Guid? selectedCompanyId = null);
}
