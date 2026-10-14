namespace CSM.Application.Compliance.Violations;

public sealed record ViolationResponse(
    Guid Id,
    string InspectionNumber,
    string SiteName,
    string Requirement,
    string? Observation,
    string? CorrectiveAction,
    string Status,
    DateOnly? DueDate,
    bool IsResolved
);