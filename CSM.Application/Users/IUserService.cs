using CSM.Application.Users.Dtos;

namespace CSM.Application.Users;

public interface IUserService
{
    Task<IReadOnlyCollection<UserResponse>> GetAllAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<UserResponse> GetByIdAsync(
        Guid currentUserId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<UserResponse> CreateAsync(
        Guid currentUserId,
        CreateUserRequest request,
        CancellationToken cancellationToken = default);

    Task<UserResponse> UpdateAsync(
        Guid currentUserId,
        Guid userId,
        UpdateUserRequest request,
        CancellationToken cancellationToken = default);

    Task SetRolesAsync(
        Guid currentUserId,
        Guid userId,
        SetUserRolesRequest request,
        CancellationToken cancellationToken = default);

    Task SetActiveStatusAsync(
        Guid currentUserId,
        Guid userId,
        bool isActive,
        CancellationToken cancellationToken = default);

    Task SetSitesAsync(
        Guid currentUserId,
        Guid userId,
        SetUserSitesRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid currentUserId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<UserProfileResponse> GetProfileAsync(
        Guid currentUserId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task ActivateAsync(
        Guid currentUserId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task DeactivateAsync(
        Guid currentUserId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<UserAuditResponse>> GetAuditAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default);
}