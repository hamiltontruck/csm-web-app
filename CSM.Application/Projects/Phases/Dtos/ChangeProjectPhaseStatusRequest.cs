using CSM.Domain.Enums;

namespace CSM.Application.Projects.Phases.Dtos;

public sealed record ChangeProjectPhaseStatusRequest(
    ProjectPhaseStatus Status);