
using System.Net.Http.Json;
using CSM.Web.Models.Security;

namespace CSM.Web.Services;

public sealed class UserSessionsService
{
    private readonly IHttpClientFactory _factory;
    private readonly IAuthService _authService;

    public UserSessionsService(
        IHttpClientFactory factory,
        IAuthService authService)
    {
        _factory = factory;
        _authService = authService;
    }

    public async Task<IReadOnlyList<UserSessionModel>> GetSessionsAsync(
        CancellationToken cancellationToken = default)
    {
        var client = _factory.CreateClient("CSM.Api");
        await _authService.ApplyAuthenticationAsync(client);

        var refreshToken = await _authService.GetRefreshTokenAsync();

        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            client.DefaultRequestHeaders.Remove("X-Current-Refresh-Token");
            client.DefaultRequestHeaders.Add(
                "X-Current-Refresh-Token",
                refreshToken);
        }

        using var response = await client.GetAsync(
            "auth/sessions",
            cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content
                   .ReadFromJsonAsync<List<UserSessionModel>>(
                       cancellationToken: cancellationToken)
               ?? [];
    }

    public async Task RevokeSessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var client = _factory.CreateClient("CSM.Api");
        await _authService.ApplyAuthenticationAsync(client);

        var refreshToken = await _authService.GetRefreshTokenAsync();

        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            client.DefaultRequestHeaders.Remove("X-Current-Refresh-Token");
            client.DefaultRequestHeaders.Add(
                "X-Current-Refresh-Token",
                refreshToken);
        }

        using var response = await client.DeleteAsync(
            $"auth/sessions/{sessionId}",
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }


public async Task<int> RevokeOtherSessionsAsync(
    CancellationToken cancellationToken = default)
{
    var client = _factory.CreateClient("CSM.Api");
    await _authService.ApplyAuthenticationAsync(client);

    var refreshToken = await _authService.GetRefreshTokenAsync();

    if (string.IsNullOrWhiteSpace(refreshToken))
    {
        throw new InvalidOperationException(
            "The current session could not be verified. Please sign in again.");
    }

    client.DefaultRequestHeaders.Remove("X-Current-Refresh-Token");
    client.DefaultRequestHeaders.Add(
        "X-Current-Refresh-Token",
        refreshToken);

    using var response = await client.PostAsync(
        "auth/sessions/revoke-others",
        content: null,
        cancellationToken);

    response.EnsureSuccessStatusCode();

    var result = await response.Content.ReadFromJsonAsync<RevokeOtherSessionsResponse>(
        cancellationToken: cancellationToken);

    return result?.RevokedCount ?? 0;
}

private sealed class RevokeOtherSessionsResponse
{
    public int RevokedCount { get; set; }
    public string? Message { get; set; }
}

}
