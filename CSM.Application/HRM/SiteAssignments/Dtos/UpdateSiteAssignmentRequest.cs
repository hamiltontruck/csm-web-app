namespace CSM.Application.HRM.SiteAssignments.Dtos;

public sealed record UpdateSiteAssignmentRequest(
    DateOnly StartDate,
    DateOnly? EndDate,
    string? RoleAtSite,
    bool IsPrimaryAssignment,
    string? Remarks);