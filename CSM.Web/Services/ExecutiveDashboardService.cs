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

    public async Task<ExecutiveDashboard?> GetDashboardAsync()
    {
        var token = await _storage.GetAsync<string>("jwt");

        if (!token.Success || string.IsNullOrWhiteSpace(token.Value))
            return null;

        var client = _factory.CreateClient("CSM.Api");

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token.Value);

        return await client.GetFromJsonAsync<ExecutiveDashboard>(
            "dashboard/executive");
    }
}