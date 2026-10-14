using CSM.Application.Compliance.Permits.Dtos;

namespace CSM.Application.Compliance.Permits;

public interface IPermitService
{
    Task<IReadOnlyCollection<PermitResponse>> GetAllAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<PermitResponse> GetByIdAsync(
        Guid currentUserId,
        Guid permitId,
        CancellationToken cancellationToken = default);

    Task<PermitResponse> CreateAsync(
        Guid currentUserId,
        CreatePermitRequest request,
        CancellationToken cancellationToken = default);

    Task<PermitResponse> UpdateAsync(
        Guid currentUserId,
        Guid permitId,
        UpdatePermitRequest request,
        CancellationToken cancellationToken = default);
}
