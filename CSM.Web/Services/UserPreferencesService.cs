
using System.Net.Http.Json;
using CSM.Web.Models.UserPreferences;

namespace CSM.Web.Services;

public sealed class UserPreferencesService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AuthService _authService;

    public UserPreferencesService(
        IHttpClientFactory httpClientFactory,
        AuthService authService)
    {
        _httpClientFactory = httpClientFactory;
        _authService = authService;
    }

    public async Task<UserPreferencesResponse?> GetAsync(
        CancellationToken cancellationToken = default)
    {
        var client = await CreateAuthenticatedClientAsync();

        using var response = await client.GetAsync(
            "user/preferences",
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<UserPreferencesResponse>(
            cancellationToken);
    }

    public async Task<UserPreferencesResponse> UpdateAsync(
        UpdateUserPreferencesRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = await CreateAuthenticatedClientAsync();

        using var response = await client.PutAsJsonAsync(
            "user/preferences",
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var message = await response.Content.ReadAsStringAsync(
                cancellationToken);

            throw new HttpRequestException(
                $"User preferences update failed ({(int)response.StatusCode}): {message}");
        }

        var result =
            await response.Content.ReadFromJsonAsync<UserPreferencesResponse>(
                cancellationToken);

        return result
            ?? throw new InvalidOperationException(
                "The user preferences API returned an empty response.");
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = _httpClientFactory.CreateClient("CSM.Api");
        await _authService.ApplyAuthenticationAsync(client);
        return client;
    }
}
