using CSM.Domain.Common;

namespace CSM.Domain.Entities.Projects;

public class WorkTaskDependency : TenantEntity
{
    public Guid WorkTaskId { get; set; }

    public Guid DependsOnTaskId { get; set; }

    public WorkTask WorkTask { get; set; } = null!;

    public WorkTask DependsOnTask { get; set; } = null!;
}