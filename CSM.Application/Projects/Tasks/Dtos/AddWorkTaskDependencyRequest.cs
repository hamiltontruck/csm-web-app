namespace CSM.Application.Projects.Tasks.Dtos;

public sealed record AddWorkTaskDependencyRequest(
    Guid DependsOnTaskId);