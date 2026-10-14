namespace CSM.Web.Models.Finance;

public sealed record CreatePettyCashAccountRequest(
    Guid? ConstructionSiteId,
    string AccountCode,
    string AccountName,
    decimal OpeningBalance);