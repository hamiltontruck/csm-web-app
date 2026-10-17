using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using CSM.Web.Models.Dashboard;

namespace CSM.Web.Services;

public class ExecutiveDashboardService : IExecutiveDashboardService
{
    private readonly IHttpClientFactory _factory;
    private readonly ProtectedSessionStorage _storage;

    public ExecutiveDashboardService(
        IHttpClientFactory factory,
        ProtectedSessionStorage storage)
    {
        _factory = factory;
        _storage = storage;
    }

    public async Task<ExecutiveDashboard?> GetDashboardAsync(Guid? selectedCompanyId = null)
    {
        var token = await _storage.GetAsync<string>("jwt");

        if (!token.Success || string.IsNullOrWhiteSpace(token.Value))
            return null;

        using var client = _factory.CreateClient("CSM.Api");

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token.Value);

        var path = selectedCompanyId.HasValue
            ? $"dashboard/executive?selectedCompanyId={selectedCompanyId.Value:D}"
            : "dashboard/executive";
        return await client.GetFromJsonAsync<ExecutiveDashboard>(path);
    }
    public async Task<IReadOnlyList<DashboardCompany>> GetCompaniesAsync()
    {
        var token = await _storage.GetAsync<string>("jwt");
        if (!token.Success || string.IsNullOrWhiteSpace(token.Value))
            return [];

        using var client = _factory.CreateClient("CSM.Api");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token.Value);
        return await client.GetFromJsonAsync<List<DashboardCompany>>("companies/lookup") ?? [];
    }
}