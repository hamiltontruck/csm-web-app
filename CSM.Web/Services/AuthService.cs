using System.Net.Http.Headers;
using System.Net.Http.Json;
using CSM.Web.Models.Auth;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace CSM.Web.Services;

public class AuthService : IAuthService
{
    private readonly IHttpClientFactory _factory;
    private readonly ProtectedSessionStorage _sessionStorage;

    private sealed class CurrentUserResponse
    {
        public string? CompanyId { get; set; }
    }

    private IReadOnlyCollection<string>? _permissions;
    private Guid? _companyId;

    public Guid? CompanyId => _companyId;

    public AuthService(
        IHttpClientFactory factory,
        ProtectedSessionStorage sessionStorage)
    {
        _factory = factory;
        _sessionStorage = sessionStorage;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        var client = _factory.CreateClient("CSM.Api");

        var response = await client.PostAsJsonAsync("auth/login", request);

        if (!response.IsSuccessStatusCode)
            return null;

        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();

        if (result is not null &&
            !string.IsNullOrWhiteSpace(result.AccessToken))
        {
            await _sessionStorage.SetAsync("jwt", result.AccessToken);
            await _sessionStorage.SetAsync("refreshToken", result.RefreshToken);
            _companyId = result.CompanyId;
            _permissions = null;
        }

        return result;
    }

    // NEW METHOD
    public async Task<string?> GetTokenAsync()
    {
        var token = await _sessionStorage.GetAsync<string>("jwt");

        if (!token.Success || string.IsNullOrWhiteSpace(token.Value))
            return null;

        return token.Value;
    }

    public async Task LogoutAsync()
    {
        await _sessionStorage.DeleteAsync("jwt");
        await _sessionStorage.DeleteAsync("refreshToken");
        await _sessionStorage.DeleteAsync("userName");
        await _sessionStorage.DeleteAsync("email");
        await _sessionStorage.DeleteAsync("roles");

        _permissions = null;
        _companyId = null;
    }

    public async Task<IReadOnlyCollection<string>> GetPermissionsAsync()
    {
        if (_permissions is not null)
            return _permissions;

        var client = _factory.CreateClient("CSM.Api");

        await ApplyAuthenticationAsync(client);

        var response =
            await client.GetAsync("auth/permissions");

        if (!response.IsSuccessStatusCode)
        {
            _permissions = Array.Empty<string>();
            return _permissions;
        }

        var permissions =
            await response.Content
                .ReadFromJsonAsync<List<string>>();

        _permissions = permissions ?? [];

        return _permissions;
    }

    public async Task ApplyAuthenticationAsync(HttpClient httpClient)
    {
        var token = await GetTokenAsync();

        httpClient.DefaultRequestHeaders.Authorization = null;

        if (!string.IsNullOrWhiteSpace(token))
        {
            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }
    }

    public async Task<Guid?> GetCurrentCompanyIdAsync()
    {
        var client = _factory.CreateClient("CSM.Api");

        await ApplyAuthenticationAsync(client);

        var response = await client.GetAsync("auth/me");

        if (!response.IsSuccessStatusCode)
            return null;

        var result =
            await response.Content.ReadFromJsonAsync<CurrentUserResponse>();

        if (result is null ||
            !Guid.TryParse(result.CompanyId, out var companyId))
        {
            return null;
        }

        return companyId;
    }

}
