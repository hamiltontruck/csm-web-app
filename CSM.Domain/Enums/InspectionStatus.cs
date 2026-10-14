namespace CSM.Domain.Enums;

public enum InspectionStatus
{
    Scheduled = 1,
    InProgress = 2,
    Passed = 3,
    PassedWithObservations = 4,
    Failed = 5,
    CorrectiveActionRequired = 6,
    Closed = 7,
    Cancelled = 8
}