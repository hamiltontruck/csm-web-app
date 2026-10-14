using System.Net.Http.Headers;
using System.Net.Http.Json;
using CSM.Web.Models.Users;

namespace CSM.Web.Services;

public class UserWebService
{
    private readonly HttpClient _http;
    private readonly IAuthService _auth;

    public UserWebService(HttpClient http, IAuthService auth)
    {
        _http = http;
        _auth = auth;
    }

    private async Task AuthorizeAsync()
    {
        var token = await _auth.GetTokenAsync();

        _http.DefaultRequestHeaders.Authorization = null;

        if (!string.IsNullOrWhiteSpace(token))
        {
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }
    }

    public async Task<List<UserModel>> GetAllAsync()
    {
        await AuthorizeAsync();
        return await _http.GetFromJsonAsync<List<UserModel>>("users")
               ?? new List<UserModel>();
    }

    public async Task<UserModel?> GetByIdAsync(Guid id)
    {
        await AuthorizeAsync();
        return await _http.GetFromJsonAsync<UserModel>($"users/{id}");
    }

    public async Task CreateAsync(CreateUserModel model)
    {
        await AuthorizeAsync();

        var response = await _http.PostAsJsonAsync("users", model);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateAsync(Guid id, UpdateUserModel model)
    {
        await AuthorizeAsync();

        var response = await _http.PutAsJsonAsync($"users/{id}", model);
        response.EnsureSuccessStatusCode();
    }

    public async Task ActivateAsync(Guid id)
    {
        await AuthorizeAsync();

        var response = await _http.PutAsync($"users/{id}/activate", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeactivateAsync(Guid id)
    {
        await AuthorizeAsync();

        var response = await _http.PutAsync($"users/{id}/deactivate", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task SetRolesAsync(Guid id, List<string> roles)
    {
        await AuthorizeAsync();

        var response = await _http.PutAsJsonAsync(
            $"users/{id}/roles",
            new { Roles = roles });

        response.EnsureSuccessStatusCode();
    }

    public async Task SetSitesAsync(
        Guid id,
        Guid? defaultSiteId,
        List<Guid> siteIds)
    {
        await AuthorizeAsync();

        var response = await _http.PutAsJsonAsync(
            $"users/{id}/sites",
            new
            {
                DefaultSiteId = defaultSiteId,
                SiteIds = siteIds.Distinct().ToList()
            });

        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteAsync(Guid id)
    {
        await AuthorizeAsync();

        var response = await _http.DeleteAsync($"users/{id}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<UserProfileModel?> GetProfileAsync(Guid id)
    {
        await AuthorizeAsync();

        return await _http.GetFromJsonAsync<UserProfileModel>(
            $"users/{id}/profile");
    }

    public async Task<List<UserAuditModel>> GetAuditAsync()
    {
        await AuthorizeAsync();

        return await _http.GetFromJsonAsync<List<UserAuditModel>>(
            "users/audit") ?? [];
    }
}