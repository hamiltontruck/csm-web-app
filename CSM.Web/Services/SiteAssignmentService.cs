using System.Net.Http.Headers;
using System.Net.Http.Json;
using CSM.Web.Models.HRM;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace CSM.Web.Services.HRM;

public sealed class SiteAssignmentService : ISiteAssignmentService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ProtectedSessionStorage _sessionStorage;

    public SiteAssignmentService(
        IHttpClientFactory httpClientFactory,
        ProtectedSessionStorage sessionStorage)
    {
        _httpClientFactory = httpClientFactory;
        _sessionStorage = sessionStorage;
    }

    private async Task<HttpClient> CreateClientAsync()
    {
        var client = _httpClientFactory.CreateClient("CSM.Api");

        var tokenResult = await _sessionStorage.GetAsync<string>("jwt");

        if (tokenResult.Success && !string.IsNullOrWhiteSpace(tokenResult.Value))
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", tokenResult.Value);
        }

        return client;
    }

    public async Task<IReadOnlyCollection<SiteAssignmentResponse>> GetAllAsync(
        Guid? employeeId = null,
        Guid? constructionSiteId = null)
    {
        var client = await CreateClientAsync();

        var query = new List<string>();

        if (employeeId.HasValue)
        {
            query.Add($"employeeId={employeeId.Value}");
        }

        if (constructionSiteId.HasValue)
        {
            query.Add($"constructionSiteId={constructionSiteId.Value}");
        }

        var url = "SiteAssignments";

        if (query.Count > 0)
        {
            url += "?" + string.Join("&", query);
        }

        var response =
            await client.GetFromJsonAsync<List<SiteAssignmentResponse>>(url);

        return response ?? [];
    }

    public async Task<SiteAssignmentResponse?> GetByIdAsync(
        Guid assignmentId)
    {
        var client = await CreateClientAsync();

        return await client.GetFromJsonAsync<SiteAssignmentResponse>(
            $"SiteAssignments/{assignmentId}");
    }

    public async Task<SiteAssignmentResponse?> CreateAsync(
        CreateSiteAssignmentRequest request)
    {
        var client = await CreateClientAsync();

        var response = await client.PostAsJsonAsync(
            "SiteAssignments",
            request);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content
            .ReadFromJsonAsync<SiteAssignmentResponse>();
    }

    public async Task<SiteAssignmentResponse?> UpdateAsync(
        Guid assignmentId,
        UpdateSiteAssignmentRequest request)
    {
        var client = await CreateClientAsync();

        var response = await client.PutAsJsonAsync(
            $"SiteAssignments/{assignmentId}",
            request);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content
            .ReadFromJsonAsync<SiteAssignmentResponse>();
    }
}