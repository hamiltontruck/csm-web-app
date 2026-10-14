namespace CSM.Application.HRM.SiteAssignments.Dtos;

public sealed record SiteAssignmentResponse(
    Guid Id,
    Guid CompanyId,
    Guid EmployeeId,
    string EmployeeNumber,
    string EmployeeName,
    Guid ConstructionSiteId,
    string SiteName,
    DateOnly StartDate,
    DateOnly? EndDate,
    string? RoleAtSite,
    bool IsPrimaryAssignment,
    string? Remarks,
    bool IsActive,
    DateTime CreatedAtUtc);