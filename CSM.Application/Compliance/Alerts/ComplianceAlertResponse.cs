namespace CSM.Application.Compliance.Alerts;

public sealed record ComplianceAlertResponse(
    Guid PermitId,
    string PermitNumber,
    string SiteName,
    string PermitHolder,
    DateOnly ExpiryDate,
    int DaysRemaining,
    bool IsExpired
);