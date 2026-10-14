using CSM.Domain.Common;
using CSM.Domain.Entities.HRM;

namespace CSM.Domain.Entities.Projects;

public sealed class TaskResourceAssignment : TenantEntity
{
    public Guid WbsTaskId { get; set; }

    public Guid EmployeeId { get; set; }

    public string Role { get; set; } = string.Empty;

    public decimal PlannedHours { get; set; }

    public decimal ActualHours { get; set; }

    public DateOnly AssignedDate { get; set; }

    public string? Remarks { get; set; }

    public WbsTask WbsTask { get; set; } = null!;

    public Employee Employee { get; set; } = null!;
}