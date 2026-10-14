namespace CSM.Web.Models.Finance;

public sealed record PettyCashAccountResponse(
    Guid Id,
    Guid CompanyId,
    Guid? ConstructionSiteId,
    string SiteName,
    string AccountCode,
    string AccountName,
    decimal OpeningBalance,
    decimal CurrentBalance,
    bool IsActive);