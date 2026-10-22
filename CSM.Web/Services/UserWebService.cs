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
        using var response = await _http.PostAsJsonAsync("users", model);
        if (response.IsSuccessStatusCode) return;

        var message = response.StatusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized => "Your session has expired. Please sign in again.",
            System.Net.HttpStatusCode.Forbidden => "You do not have permission to create users.",
            _ => "Unable to create user."
        };
        if ((int)response.StatusCode >= 500)
            message = "The user service is temporarily unavailable. Please try again in a minute.";
        else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            try
            {
                using var body = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                if (body.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object &&
                    body.RootElement.TryGetProperty("message", out var value) &&
                    value.ValueKind == System.Text.Json.JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(value.GetString()))
                    message = value.GetString()!;
            }
            catch (System.Text.Json.JsonException) { }
        }
        throw new HttpRequestException(message, null, response.StatusCode);
    }

    public async Task UpdateAsync(Guid id, UpdateUserModel model)
    {
        await AuthorizeAsync();

        using var response = await _http.PutAsJsonAsync($"users/{id}", model);
        await EnsureUserChangeSuccessAsync(response);
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

        using var response = await _http.PutAsJsonAsync(
            $"users/{id}/roles",
            new { Roles = roles });

        await EnsureUserChangeSuccessAsync(response);
    }

    public async Task SetSitesAsync(
        Guid id,
        Guid? defaultSiteId,
        List<Guid> siteIds)
    {
        await AuthorizeAsync();

        using var response = await _http.PutAsJsonAsync(
            $"users/{id}/sites",
            new
            {
                DefaultSiteId = defaultSiteId,
                SiteIds = siteIds.Distinct().ToList()
            });

        await EnsureUserChangeSuccessAsync(response);
    }

    private static async Task EnsureUserChangeSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        var message = response.StatusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized => "Your session has expired. Please sign in again.",
            System.Net.HttpStatusCode.Forbidden => "You do not have permission to edit this user.",
            _ => "Unable to save user changes. Please try again."
        };
        if ((int)response.StatusCode >= 500)
            message = "The user service is temporarily unavailable. Please try again in a minute.";
        else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            try
            {
                using var body = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                if (body.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object &&
                    body.RootElement.TryGetProperty("message", out var value) &&
                    value.ValueKind == System.Text.Json.JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(value.GetString()))
                    message = value.GetString()!;
            }
            catch (System.Text.Json.JsonException) { }
        }
        throw new HttpRequestException(message, null, response.StatusCode);
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