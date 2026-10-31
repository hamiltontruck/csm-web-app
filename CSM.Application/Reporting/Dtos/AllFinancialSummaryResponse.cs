using System.Text.Json.Serialization;
namespace CSM.Application.Reporting.Dtos;

public sealed record AllFinancialSummaryResponse(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] FinancialSummaryResponse? Company,
    IReadOnlyCollection<SiteFinancialSummaryResponse> Sites,
    FinancialSummaryResponse Total
);

public sealed record SiteFinancialSummaryResponse(
    Guid SiteId,
    string SiteName,
    string SiteCode,
    FinancialSummaryResponse Financial
);