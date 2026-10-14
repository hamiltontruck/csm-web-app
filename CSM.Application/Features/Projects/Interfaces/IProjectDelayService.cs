using CSM.Application.Features.Projects.DTOs;

namespace CSM.Application.Features.Projects.Interfaces;

public interface IProjectDelayService
{
    Task<List<ProjectDelayResponse>> GetAllAsync(
        Guid companyId,
        Guid? projectId = null,
        CancellationToken cancellationToken = default);

    Task<ProjectDelayResponse> GetByIdAsync(
        Guid companyId,
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ProjectDelayResponse> CreateAsync(
        Guid companyId,
        CreateProjectDelayRequest request,
        CancellationToken cancellationToken = default);

    Task<ProjectDelayResponse> ResolveAsync(
        Guid companyId,
        Guid id,
        string? actionTaken,
        CancellationToken cancellationToken = default);
}