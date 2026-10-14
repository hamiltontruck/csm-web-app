using System.Net.Http.Headers;
using System.Net.Http.Json;
using CSM.Web.Models.Reporting;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace CSM.Web.Services;

public sealed class ReportingService : IReportingService
{
    private readonly IHttpClientFactory _factory;
    private readonly ProtectedSessionStorage _storage;

    public ReportingService(
        IHttpClientFactory factory,
        ProtectedSessionStorage storage)
    {
        _factory = factory;
        _storage = storage;
    }

    private async Task<HttpClient> ClientAsync()
    {
        var client = _factory.CreateClient("CSM.Api");

        var token = await _storage.GetAsync<string>("jwt");

        if (token.Success &&
            !string.IsNullOrWhiteSpace(token.Value))
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    token.Value);
        }

        return client;
    }

    public async Task<ExecutiveDashboardResponse>
        GetExecutiveDashboardAsync()
    {
        var client = await ClientAsync();

        using var response =
            await client.GetAsync(
                "Reports/executive-dashboard");

        if (!response.IsSuccessStatusCode)
        {
            var error =
                await response.Content.ReadAsStringAsync();

            throw new InvalidOperationException(
                $"Failed to load executive dashboard. " +
                $"Status: {(int)response.StatusCode}. {error}");
        }

        return await response.Content
                   .ReadFromJsonAsync<ExecutiveDashboardResponse>()
               ?? throw new InvalidOperationException(
                   "The API returned an empty executive dashboard response.");
    }

    public async Task<FinancialSummaryResponse>
        GetFinancialSummaryAsync()
    {
        var client = await ClientAsync();

        using var response =
            await client.GetAsync(
                "Reports/financial-summary");

        if (!response.IsSuccessStatusCode)
        {
            var error =
                await response.Content.ReadAsStringAsync();

            throw new InvalidOperationException(
                $"Failed to load financial summary. " +
                $"Status: {(int)response.StatusCode}. {error}");
        }

        return await response.Content
                   .ReadFromJsonAsync<FinancialSummaryResponse>()
               ?? throw new InvalidOperationException(
                   "The API returned an empty financial summary response.");
    }

    public async Task<AllFinancialSummaryResponse>
        GetAllFinancialSummariesAsync()
    {
        var client = await ClientAsync();

        using var response =
            await client.GetAsync(
                "Reports/financial-summary/all");

        if (!response.IsSuccessStatusCode)
        {
            var error =
                await response.Content.ReadAsStringAsync();

            throw new InvalidOperationException(
                $"Failed to load all financial summaries. " +
                $"Status: {(int)response.StatusCode}. {error}");
        }

        return await response.Content
                   .ReadFromJsonAsync<AllFinancialSummaryResponse>()
               ?? throw new InvalidOperationException(
                   "The API returned an empty all financial summaries response.");
    }

    public async Task<ExecutiveDashboardResponse>
        GetSiteExecutiveDashboardAsync(
            Guid constructionSiteId)
    {
        var client = await ClientAsync();

        using var response =
            await client.GetAsync(
                $"Reports/site/{constructionSiteId}/executive-dashboard");

        if (!response.IsSuccessStatusCode)
        {
            var error =
                await response.Content.ReadAsStringAsync();

            throw new InvalidOperationException(
                $"Failed to load site executive dashboard. " +
                $"Status: {(int)response.StatusCode}. {error}");
        }

        return await response.Content
                   .ReadFromJsonAsync<ExecutiveDashboardResponse>()
               ?? throw new InvalidOperationException(
                   "The API returned an empty site executive dashboard response.");
    }

    public async Task<FinancialSummaryResponse>
        GetSiteFinancialSummaryAsync(
            Guid constructionSiteId)
    {
        var client = await ClientAsync();

        using var response =
            await client.GetAsync(
                $"Reports/site/{constructionSiteId}/financial-summary");

        if (!response.IsSuccessStatusCode)
        {
            var error =
                await response.Content.ReadAsStringAsync();

            throw new InvalidOperationException(
                $"Failed to load site financial summary. " +
                $"Status: {(int)response.StatusCode}. {error}");
        }

        return await response.Content
                   .ReadFromJsonAsync<FinancialSummaryResponse>()
               ?? throw new InvalidOperationException(
                   "The API returned an empty site financial summary response.");
    }
}