
using System.Net.Http.Json;
using CSM.Web.Models.Security;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace CSM.Web.Services;

public sealed class UserSessionsService
{
    private readonly IHttpClientFactory _factory;
    private readonly IAuthService _authService;
    private readonly ProtectedSessionStorage _sessionStorage;

    public UserSessionsService(
        IHttpClientFactory factory,
        IAuthService authService,
        ProtectedSessionStorage sessionStorage)
    {
        _factory = factory;
        _authService = authService;
        _sessionStorage = sessionStorage;
    }

    public async Task<IReadOnlyList<UserSessionModel>> GetSessionsAsync(
        CancellationToken cancellationToken = default)
    {
        var client = _factory.CreateClient("CSM.Api");
        await _authService.ApplyAuthenticationAsync(client);

        var refreshToken = await GetRefreshTokenAsync();

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

        var refreshToken = await GetRefreshTokenAsync();

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

    var refreshToken = await GetRefreshTokenAsync();

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

private async Task<string?> GetRefreshTokenAsync()
{
    var result = await _sessionStorage.GetAsync<string>("refreshToken");
    return result.Success && !string.IsNullOrWhiteSpace(result.Value)
        ? result.Value
        : null;
}

private sealed class RevokeOtherSessionsResponse
{
    public int RevokedCount { get; set; }
    public string? Message { get; set; }
}

}
