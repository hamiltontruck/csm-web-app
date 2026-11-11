
using System.Net.Http.Json;
using CSM.Web.Models.UserPreferences;

namespace CSM.Web.Services;

public sealed class UserPreferencesService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAuthService _authService;
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    public UserPreferencesResponse? Saved { get; private set; }
    public event Action? Changed;
    private void Remember(UserPreferencesResponse? value) { Saved = value; Changed?.Invoke(); }

    public UserPreferencesService(
        IHttpClientFactory httpClientFactory,
        IAuthService authService)
    {
        _httpClientFactory = httpClientFactory;
        _authService = authService;
    }

    public async Task<UserPreferencesResponse> GetAsync(
        CancellationToken cancellationToken = default)
    {
        await _loadGate.WaitAsync(cancellationToken);
        try
        {
            Remember(null);
            var client = await CreateAuthenticatedClientAsync();
            using var response = await client.GetAsync("user/preferences", cancellationToken);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<UserPreferencesResponse>(cancellationToken)
                ?? throw new InvalidOperationException("The preferences API returned an empty response.");
            Remember(result);
            return result;
        }
        finally { _loadGate.Release(); }
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
            throw new HttpRequestException(
                $"User preferences update failed ({(int)response.StatusCode}).", null, response.StatusCode);
        }

        var result =
            await response.Content.ReadFromJsonAsync<UserPreferencesResponse>(
                cancellationToken);

        if (result is null) throw new InvalidOperationException("The preferences API returned an empty response.");
        Remember(result);
        return result;
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = _httpClientFactory.CreateClient("CSM.Api");
        await _authService.ApplyAuthenticationAsync(client);
        return client;
    }
}
