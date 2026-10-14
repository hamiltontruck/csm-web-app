namespace CSM.Application.Users.Dtos;

public sealed record UserAuditResponse(
    Guid Id,
    DateTime PerformedAtUtc,
    string ActorName,
    string Action,
    string TargetName,
    string Details
);