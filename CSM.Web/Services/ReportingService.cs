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
        GetExecutiveDashboardAsync(Guid? selectedCompanyId = null)
    {
        var client = await ClientAsync();

        using var response =
            await client.GetAsync(
                WithCompany("Reports/executive-dashboard", selectedCompanyId));

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
        GetFinancialSummaryAsync(Guid? selectedCompanyId = null)
    {
        var client = await ClientAsync();

        using var response =
            await client.GetAsync(
                WithCompany("Reports/financial-summary", selectedCompanyId));

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
        GetAllFinancialSummariesAsync(Guid? selectedCompanyId = null)
    {
        var client = await ClientAsync();

        using var response =
            await client.GetAsync(
                WithCompany("Reports/financial-summary/all", selectedCompanyId));

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
            Guid constructionSiteId, Guid? selectedCompanyId = null)
    {
        var client = await ClientAsync();

        using var response =
            await client.GetAsync(
                WithCompany($"Reports/site/{constructionSiteId}/executive-dashboard", selectedCompanyId));

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
            Guid constructionSiteId, Guid? selectedCompanyId = null)
    {
        var client = await ClientAsync();

        using var response =
            await client.GetAsync(
                WithCompany($"Reports/site/{constructionSiteId}/financial-summary", selectedCompanyId));

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
    private static string WithCompany(string path, Guid? companyId)
        => companyId.HasValue ? $"{path}?selectedCompanyId={companyId.Value:D}" : path;
}
