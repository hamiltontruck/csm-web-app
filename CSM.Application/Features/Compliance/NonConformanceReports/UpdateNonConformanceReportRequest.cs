using CSM.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace CSM.Application.Features.Compliance.NonConformanceReports;

public class UpdateNonConformanceReportRequest
{
    [Required]
    public Guid ConstructionSiteId { get; set; }

    public Guid? ProjectId { get; set; }

    public Guid? AssignedToEmployeeId { get; set; }

    [Required, MaxLength(50)]
    public string NcrNumber { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Location { get; set; } = string.Empty;

    public NcrSeverity Severity { get; set; }

    public NcrStatus Status { get; set; }

    public DateTime ReportedDate { get; set; }

    public DateTime? DueDate { get; set; }

    public DateTime? ClosedDate { get; set; }

    public string? RootCause { get; set; }

    public string? CorrectiveAction { get; set; }

    public string? PreventiveAction { get; set; }
}