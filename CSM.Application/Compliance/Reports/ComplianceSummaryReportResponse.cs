namespace CSM.Application.Compliance.Reports;

public sealed record ComplianceSummaryReportResponse(
    int TotalInspections,
    int CompliantItems,
    int NonCompliantItems,
    int OpenCorrectiveActions,
    int ActivePermits,
    int ExpiredPermits,
    int SafetyIncidents,
    int SafetyObservations
);