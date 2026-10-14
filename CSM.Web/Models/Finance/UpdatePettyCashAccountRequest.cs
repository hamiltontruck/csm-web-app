namespace CSM.Web.Models.Finance;

public sealed record UpdatePettyCashAccountRequest(
    string AccountName,
    bool IsActive);