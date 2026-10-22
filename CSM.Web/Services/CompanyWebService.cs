using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CSM.Web.Models.Companies;

namespace CSM.Web.Services;

public sealed class CompanyWebService(HttpClient http, IAuthService auth)
{
    public async Task<bool> IsSuperAdminAsync()
    {
        using var response = await SendAsync(HttpMethod.Get, "auth/me");
        if (!response.IsSuccessStatusCode) return false;
        var identity = await response.Content.ReadFromJsonAsync<CompanyIdentity>();
        return identity?.Roles.Contains("SuperAdmin", StringComparer.Ordinal) == true;
    }

    public async Task<List<CompanyModel>> GetAllAsync()
    {
        using var response = await SendAsync(HttpMethod.Get, "companies");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<CompanyModel>>() ?? [];
    }

    public async Task<CompanyModel> CreateAsync(CreateCompanyModel model)
    {
        using var response = await SendAsync(HttpMethod.Post, "companies", model);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<CompanyModel>()
            ?? throw new HttpRequestException("The company service returned an empty response.");
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, CreateCompanyModel? model = null)
    {
        using var request = new HttpRequestMessage(method, path);
        var token = await auth.GetTokenAsync();
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (model is not null) request.Content = JsonContent.Create(model);
        return await http.SendAsync(request);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        var message = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Your session is missing or has expired. Please sign in again.",
            HttpStatusCode.Forbidden => "You do not have permission to manage companies.",
            HttpStatusCode.BadRequest or HttpStatusCode.Conflict => "Unable to save company. Check the company details.",
            _ => "The company service is temporarily unavailable. Please try again in a minute."
        };
        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
        {
            try
            {
                using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                if (body.RootElement.ValueKind == JsonValueKind.Object &&
                    body.RootElement.TryGetProperty("message", out var value) &&
                    value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))
                    message = value.GetString()!;
            }
            catch (JsonException) { }
        }
        throw new HttpRequestException(message, null, response.StatusCode);
    }

    private sealed class CompanyIdentity
    {
        public string[] Roles { get; set; } = [];
    }
}
