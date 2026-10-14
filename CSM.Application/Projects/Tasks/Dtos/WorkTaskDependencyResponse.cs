namespace CSM.Application.Projects.Tasks.Dtos;

public sealed record WorkTaskDependencyResponse(
    Guid Id,
    Guid WorkTaskId,
    Guid DependsOnTaskId);