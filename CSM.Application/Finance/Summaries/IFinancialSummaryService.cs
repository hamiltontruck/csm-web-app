using CSM.Application.Finance.Summaries.Dtos;

namespace CSM.Application.Finance.Summaries;

public interface IFinancialSummaryService
{
    Task<CompanyFinancialSummaryResponse> GetCompanySummaryAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<SiteFinancialSummaryResponse> GetSiteSummaryAsync(
        Guid currentUserId,
        Guid constructionSiteId,
        CancellationToken cancellationToken = default);
}