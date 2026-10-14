using System.ComponentModel.DataAnnotations;

namespace CSM.Application.Projects.ResourceAssignments.Dtos;

public sealed class CreateTaskResourceAssignmentRequest
{
    [Required]
    public Guid WbsTaskId { get; set; }

    [Required]
    public Guid EmployeeId { get; set; }

    [Required, MaxLength(100)]
    public string Role { get; set; } = string.Empty;

    [Range(0, 9999)]
    public decimal PlannedHours { get; set; }

    [Range(0, 9999)]
    public decimal ActualHours { get; set; }

    public DateOnly AssignedDate { get; set; }

    [MaxLength(500)]
    public string? Remarks { get; set; }
}