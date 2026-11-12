namespace CSM.Application.Finance.Options;

public interface IFinanceSiteOptionsService
{
    Task<FinanceSiteOptionsResponse> GetAsync(Guid actorId, Guid siteId, string purpose, CancellationToken cancellationToken = default);
}

public sealed record FinanceSiteOptionsResponse(Guid SiteId, Guid CompanyId, string SiteCode, string SiteName,
    IReadOnlyCollection<FinanceProjectOption> Projects, IReadOnlyCollection<FinanceCostCodeOption> CostCodes,
    IReadOnlyCollection<FinanceVendorOption> Vendors);
public sealed record FinanceProjectOption(Guid Id, string ProjectCode, string Name, Guid ConstructionSiteId);
public sealed record FinanceCostCodeOption(Guid Id, string Code, string Name, Guid? ConstructionSiteId, bool IsActive);
public sealed record FinanceVendorOption(Guid Id, string VendorCode, string Name, bool IsActive);
