namespace CSM.Application.Projects.ResourceAssignments.Dtos;

public sealed class TaskResourceAssignmentResponse
{
    public Guid Id { get; set; }

    public Guid WbsTaskId { get; set; }

    public Guid EmployeeId { get; set; }

    public string EmployeeName { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public decimal PlannedHours { get; set; }

    public decimal ActualHours { get; set; }

    public DateOnly AssignedDate { get; set; }

    public string? Remarks { get; set; }
}