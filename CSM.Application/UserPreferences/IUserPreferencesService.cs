using CSM.Application.UserPreferences.Dtos;

namespace CSM.Application.UserPreferences;

public interface IUserPreferencesService
{
    Task<UserPreferencesResponse> GetAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<UserPreferencesResponse> UpdateAsync(
        Guid currentUserId,
        UpdateUserPreferencesRequest request,
        CancellationToken cancellationToken = default);
}