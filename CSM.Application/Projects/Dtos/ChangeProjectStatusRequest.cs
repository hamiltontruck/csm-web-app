using CSM.Domain.Enums;

namespace CSM.Application.Projects.Dtos;

public sealed record ChangeProjectStatusRequest(
    ProjectStatus Status);