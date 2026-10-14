namespace CSM.Application.HRM.SiteAssignments.Dtos;

public sealed record CreateSiteAssignmentRequest(
    Guid EmployeeId,
    Guid ConstructionSiteId,
    DateOnly StartDate,
    DateOnly? EndDate,
    string? RoleAtSite,
    bool IsPrimaryAssignment,
    string? Remarks);