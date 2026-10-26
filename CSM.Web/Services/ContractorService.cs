using System.Net.Http.Headers;
using System.Net.Http.Json;
using CSM.Web.Models.HRM;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace CSM.Web.Services;

public sealed class ContractorService : IContractorService
{
    private readonly IHttpClientFactory _factory;
    private readonly ProtectedSessionStorage _storage;

    public ContractorService(
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

    public async Task<IReadOnlyList<ContractorResponse>>
        GetContractorsAsync(bool? isActive = null)
    {
        var client = await ClientAsync();

        var url = "Contractors";

        if (isActive.HasValue)
        {
            url += $"?isActive={isActive.Value.ToString().ToLowerInvariant()}";
        }

        using var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<
            IReadOnlyList<ContractorResponse>>() ?? [];
    }

    public async Task<ContractorResponse> GetContractorAsync(
        Guid contractorId)
    {
        var client = await ClientAsync();

        return await client.GetFromJsonAsync<ContractorResponse>(
            $"Contractors/{contractorId}")
            ?? throw new Exception(
                "Failed to read contractor details.");
    }

    public async Task<ContractorResponse> CreateContractorAsync(
        CreateContractorRequest request)
    {
        var client = await ClientAsync();

        var response = await client.PostAsJsonAsync(
            "Contractors",
            request);

        if (!response.IsSuccessStatusCode)
        {
            var error =
                await response.Content.ReadAsStringAsync();

            throw new Exception(
                $"Contractor creation failed. " +
                $"HTTP {(int)response.StatusCode} " +
                $"{response.StatusCode}: {error}");
        }

        return await response.Content
            .ReadFromJsonAsync<ContractorResponse>()
            ?? throw new Exception(
                "Failed to read contractor response.");
    }

    public async Task<ContractorResponse> UpdateContractorAsync(
        Guid contractorId,
        UpdateContractorRequest request)
    {
        var client = await ClientAsync();

        var response = await client.PutAsJsonAsync(
            $"Contractors/{contractorId}",
            request);

        if (!response.IsSuccessStatusCode)
        {
            var error =
                await response.Content.ReadAsStringAsync();

            throw new Exception(
                $"Contractor update failed. " +
                $"HTTP {(int)response.StatusCode} " +
                $"{response.StatusCode}: {error}");
        }

        return await response.Content
            .ReadFromJsonAsync<ContractorResponse>()
            ?? throw new Exception(
                "Failed to read updated contractor response.");
    }
}