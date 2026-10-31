namespace CSM.Web.Models.Reporting;

public sealed class AllFinancialSummaryResponse
{
    public FinancialSummaryResponse? Company { get; set; }

    public List<SiteFinancialSummaryResponse> Sites { get; set; } = [];

    public FinancialSummaryResponse Total { get; set; } = new();
}

public sealed class SiteFinancialSummaryResponse
{
    public Guid SiteId { get; set; }

    public string SiteName { get; set; } = string.Empty;

    public string SiteCode { get; set; } = string.Empty;

    public FinancialSummaryResponse Financial { get; set; } = new();
}