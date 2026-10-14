using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using CSM.Web.Models.Projects;
using CSM.Web.Models.Sites;
using System.Text.Json;
using System.Text.Json.Serialization;
using CSM.Web.Models.Equipment;

namespace CSM.Web.Services;

public class ProjectService : IProjectService
{
    private readonly IHttpClientFactory _factory;
    private readonly ProtectedSessionStorage _storage;

    public ProjectService(
        IHttpClientFactory factory,
        ProtectedSessionStorage storage)
    {
        _factory = factory;
        _storage = storage;
    }

    private async Task<HttpClient> CreateAuthorizedClientAsync()
    {
        var client = _factory.CreateClient("CSM.Api");

        var token = await _storage.GetAsync<string>("jwt");

        if (token.Success && !string.IsNullOrWhiteSpace(token.Value))
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token.Value);
        }

        return client;
    }

    public async Task<bool> CreateProjectAsync(CreateProjectRequest request)
    {
        var client = _factory.CreateClient("CSM.Api");

        var token = await _storage.GetAsync<string>("jwt");

        if (token.Success && !string.IsNullOrWhiteSpace(token.Value))
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token.Value);
        }

        var response = await client.PostAsJsonAsync("projects", request);

        return response.IsSuccessStatusCode;
    }

    public async Task<List<ProjectSummary>> GetProjectsAsync()
    {
        var token = await _storage.GetAsync<string>("jwt");

        if (!token.Success || string.IsNullOrWhiteSpace(token.Value))
            return [];

        var client = _factory.CreateClient("CSM.Api");

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token.Value);

        var result = await client.GetFromJsonAsync<List<ProjectSummary>>(
            "projects");

        return result ?? [];
    }

    public async Task<ProjectDetail?> GetProjectAsync(Guid id)
    {
        var token = await _storage.GetAsync<string>("jwt");

        if (!token.Success || string.IsNullOrWhiteSpace(token.Value))
            return null;

        var client = _factory.CreateClient("CSM.Api");

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token.Value);

        return await client.GetFromJsonAsync<ProjectDetail>(
            $"projects/{id}");
    }

    public async Task<List<WorkTask>> GetWorkTasksAsync(Guid projectId)
    {
        var token = await _storage.GetAsync<string>("jwt");

        if (!token.Success || string.IsNullOrWhiteSpace(token.Value))
            return [];

        var client = _factory.CreateClient("CSM.Api");

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token.Value);

        var result = await client.GetFromJsonAsync<List<WorkTask>>(
            $"projects/{projectId}/tasks");

        return result ?? [];
    }

    public async Task<(bool Success, string Message)> CreateTaskAsync(CreateWorkTaskRequest request)
    {
        var token = await _storage.GetAsync<string>("jwt");

        if (!token.Success || string.IsNullOrWhiteSpace(token.Value))
            return (false, "JWT token not found.");

        var client = _factory.CreateClient("CSM.Api");

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Value);

        var response = await client.PostAsJsonAsync(
            $"projects/{request.ProjectId}/tasks",
            request);

        if (response.IsSuccessStatusCode)
            return (true, "");

        var error = await response.Content.ReadAsStringAsync();
        return (false, $"HTTP {(int)response.StatusCode}: {error}");
    }

    public async Task<bool> UpdateProjectAsync(
        Guid projectId,
        UpdateProjectRequest request)
    {
        var token = await _storage.GetAsync<string>("jwt");

        if (!token.Success || string.IsNullOrWhiteSpace(token.Value))
            return false;

        var client = _factory.CreateClient("CSM.Api");

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer",
                token.Value);

        var response = await client.PutAsJsonAsync(
            $"projects/{projectId}",
            request);

        return response.IsSuccessStatusCode;
    }

    public async Task<List<ProjectPhase>> GetProjectPhasesAsync(Guid projectId)
    {
        var token = await _storage.GetAsync<string>("jwt");

        if (!token.Success || string.IsNullOrWhiteSpace(token.Value))
            return [];

        var client = _factory.CreateClient("CSM.Api");

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token.Value);

        return await client.GetFromJsonAsync<List<ProjectPhase>>(
            $"projectphases?projectId={projectId}") ?? [];
    }

    public async Task<bool> CreateProjectPhaseAsync(CreateProjectPhaseRequest request)
    {
        var client = await CreateAuthorizedClientAsync();

        var response = await client.PostAsJsonAsync(
            "ProjectPhases",
            request);

        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateProjectPhaseAsync(
        Guid phaseId,
        UpdateProjectPhaseRequest request)
    {
        var token = await _storage.GetAsync<string>("jwt");

        if (!token.Success || string.IsNullOrWhiteSpace(token.Value))
            return false;

        var client = _factory.CreateClient("CSM.Api");

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token.Value);

        var response = await client.PutAsJsonAsync(
            $"projectphases/{phaseId}",
            request);

        return response.IsSuccessStatusCode;
    }

    public async Task<List<ProjectMilestone>> GetProjectMilestonesAsync(Guid projectId)
    {
        var token = await _storage.GetAsync<string>("jwt");

        if (!token.Success || string.IsNullOrWhiteSpace(token.Value))
            return [];

        var client = _factory.CreateClient("CSM.Api");

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token.Value);

        return await client.GetFromJsonAsync<List<ProjectMilestone>>(
            $"projectmilestones?projectId={projectId}") ?? [];
    }

    public async Task<bool> CreateProjectMilestoneAsync(
        CreateProjectMilestoneRequest request)
    {
        var token = await _storage.GetAsync<string>("jwt");

        if (!token.Success || string.IsNullOrWhiteSpace(token.Value))
            throw new Exception("JWT token missing.");

        var client = _factory.CreateClient("CSM.Api");

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token.Value);

        var response = await client.PostAsJsonAsync(
            "projectmilestones",
            request);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception(error);
        }

        return true;
    }

    public async Task<List<SiteResponse>> GetSitesAsync()
    {
        var client = _factory.CreateClient("CSM.Api");

        var token = await _storage.GetAsync<string>("jwt");

        if (token.Success && !string.IsNullOrWhiteSpace(token.Value))
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token.Value);
        }

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        options.Converters.Add(new JsonStringEnumConverter());

        return await client.GetFromJsonAsync<List<SiteResponse>>(
                   "sites",
                   options)
               ?? [];
    }

    public async Task<List<SiteResponse>> GetSitesForAssignmentAsync()
    {
        var client = _factory.CreateClient("CSM.Api");

        var token = await _storage.GetAsync<string>("jwt");

        if (token.Success && !string.IsNullOrWhiteSpace(token.Value))
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    token.Value);
        }

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        options.Converters.Add(new JsonStringEnumConverter());

        return await client.GetFromJsonAsync<List<SiteResponse>>(
                   "sites/assignment-options",
                   options)
               ?? [];
    }

    public async Task<List<ProjectResponse>> GetProjectsBySiteAsync(
        Guid constructionSiteId)
    {
        var token = await _storage.GetAsync<string>("jwt");

        if (!token.Success || string.IsNullOrWhiteSpace(token.Value))
            return [];

        var client = _factory.CreateClient("CSM.Api");

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token.Value);

        return await client.GetFromJsonAsync<List<ProjectResponse>>(
            $"projects?constructionSiteId={constructionSiteId}") ?? [];
    }

    public async Task<List<DailyProgressLogResponse>> GetDailyProgressLogsAsync(
        Guid projectId)
    {
        var token = await _storage.GetAsync<string>("jwt");

        if (!token.Success || string.IsNullOrWhiteSpace(token.Value))
            return [];

        var client = _factory.CreateClient("CSM.Api");

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token.Value);

        return await client.GetFromJsonAsync<List<DailyProgressLogResponse>>(
            $"dailyprogresslogs?projectId={projectId}") ?? [];
    }

    public async Task CreateSiteAsync(CreateSiteRequest request)
    {
        var client = _factory.CreateClient("CSM.Api");

        var token = await _storage.GetAsync<string>("jwt");

        if (token.Success && !string.IsNullOrWhiteSpace(token.Value))
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token.Value);
        }

        var response = await client.PostAsJsonAsync("sites", request);

        if (!response.IsSuccessStatusCode)
        {
            var message = await response.Content.ReadAsStringAsync();
            throw new Exception(message);
        }
    }

    public async Task<List<BusinessUnitModel>> GetBusinessUnitsAsync()
    {
        var client = _factory.CreateClient("CSM.Api");

        var token = await _storage.GetAsync<string>("jwt");

        if (token.Success && !string.IsNullOrWhiteSpace(token.Value))
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token.Value);
        }

        return await client.GetFromJsonAsync<List<BusinessUnitModel>>(
                   "businessunits")
               ?? [];
    }

    public async Task<SiteResponse> GetSiteAsync(Guid siteId)
    {
        var client = await CreateAuthorizedClientAsync();

        return await client.GetFromJsonAsync<SiteResponse>($"sites/{siteId}")
               ?? throw new Exception("Site not found.");
    }

    public async Task UpdateSiteAsync(Guid siteId, UpdateSiteRequest request)
    {
        var client = await CreateAuthorizedClientAsync();

        var response = await client.PutAsJsonAsync($"sites/{siteId}", request);

        response.EnsureSuccessStatusCode();
    }

    public async Task<SiteResponse> ChangeSiteStatusAsync(Guid siteId, SiteStatus status)
    {
        var client = await CreateAuthorizedClientAsync();

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        options.Converters.Add(new JsonStringEnumConverter());

        var response = await client.PutAsJsonAsync(
            $"sites/{siteId}/status",
            new ChangeSiteStatusRequest
            {
                Status = status
            },
            options);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<SiteResponse>(options)
               ?? throw new Exception("Unable to read updated site.");
    }

    public async Task CreateDailyProgressAsync(CreateDailyProgressRequest request)
    {
        var client = await CreateAuthorizedClientAsync();

        var response = await client.PostAsJsonAsync(
            "DailyProgressLogs",
            request);

        if (response.IsSuccessStatusCode)
            return;

        var error = await response.Content.ReadAsStringAsync();
        throw new Exception(error);
    }

    public async Task<List<DailyProgressLogResponse>>
        GetTaskDailyProgressAsync(Guid projectId)
    {
        var client = await CreateAuthorizedClientAsync();

        return await client.GetFromJsonAsync<List<DailyProgressLogResponse>>(
            $"DailyProgressLogs?projectId={projectId}")
            ?? new();
    }

    public async Task<DailyProgressTaskEntryResponse> CreateTaskProgressEntryAsync(
        CreateDailyProgressTaskEntryRequest request)
    {
        var client = await CreateAuthorizedClientAsync();

        var response = await client.PostAsJsonAsync(
            "DailyProgressTaskEntries",
            request);

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<DailyProgressTaskEntryResponse>())!;
    }

    public async Task<Guid> GetOrCreateDailyLogAsync(CreateDailyProgressRequest request)
    {
        var client = await CreateAuthorizedClientAsync();

        var response = await client.PostAsJsonAsync(
            "DailyProgressLogs",
            request);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception(error);
        }

        var log = await response.Content
            .ReadFromJsonAsync<DailyProgressLogResponse>();

        return log!.Id;
    }

    public async Task<List<DailyProgressTaskEntryResponse>> GetTaskProgressEntriesAsync(Guid dailyLogId)
    {
        var client = await CreateAuthorizedClientAsync();

        return await client.GetFromJsonAsync<List<DailyProgressTaskEntryResponse>>(
            $"DailyProgressTaskEntries/{dailyLogId}") ?? new();
    }
    public async Task AddEquipmentEntryAsync(CreateEquipmentEntryRequest request)
    {
        var client = await CreateAuthorizedClientAsync();

        var response = await client.PostAsJsonAsync(
            "DailyProgressTaskEntries/equipment",
            request);

        response.EnsureSuccessStatusCode();
    }

    public async Task<List<EquipmentSummary>> GetEquipmentAsync()
    {
        var client = await CreateAuthorizedClientAsync();

        return await client.GetFromJsonAsync<List<EquipmentSummary>>(
            "Equipment") ?? new();
    }



}