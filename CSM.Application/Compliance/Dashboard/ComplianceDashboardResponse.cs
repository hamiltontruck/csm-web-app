namespace CSM.Application.Compliance.Dashboard;

public sealed record ComplianceDashboardResponse(
    int TotalInspections,
    int OpenCorrectiveActions,
    int ActivePermits,
    int ExpiringPermits,
    int OpenNcrs,
    int SafetyIncidents,
    int SafetyObservations
);