using CSM.Domain.Enums;

namespace CSM.Application.Projects.Tasks.Dtos;

public sealed record ChangeWorkTaskStatusRequest(
    WorkTaskStatus Status);