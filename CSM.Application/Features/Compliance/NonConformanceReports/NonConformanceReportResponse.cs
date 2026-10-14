namespace CSM.Application.Features.Compliance.NonConformanceReports;

using CSM.Domain.Enums;

public class NonConformanceReportResponse
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }

    public Guid ConstructionSiteId { get; set; }
    public string ConstructionSiteName { get; set; } = string.Empty;

    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }

    public Guid RaisedById { get; set; }
    public string RaisedByName { get; set; } = string.Empty;

    public Guid? AssignedToEmployeeId { get; set; }
    public string? AssignedToEmployeeName { get; set; }

    public string NcrNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;

    public NcrSeverity Severity { get; set; }
    public NcrStatus Status { get; set; }

    public DateTime ReportedDate { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? ClosedDate { get; set; }

    public string? RootCause { get; set; }
    public string? CorrectiveAction { get; set; }
    public string? PreventiveAction { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}