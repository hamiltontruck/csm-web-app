using CSM.Application.Reporting.Dtos;

namespace CSM.Application.Reporting;

public interface IReportingService
{
    Task<ExecutiveDashboardResponse> GetExecutiveDashboardAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<FinancialSummaryResponse> GetFinancialSummaryAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<ExecutiveDashboardResponse> GetSiteExecutiveDashboardAsync(
        Guid currentUserId,
        Guid constructionSiteId,
        CancellationToken cancellationToken = default);

    Task<FinancialSummaryResponse> GetSiteFinancialSummaryAsync(
        Guid currentUserId,
        Guid constructionSiteId,
        CancellationToken cancellationToken = default);

    Task<AllFinancialSummaryResponse> GetAllFinancialSummariesAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default);
}
