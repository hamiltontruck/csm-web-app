using System.Net.Http.Headers;
using System.Net.Http.Json;
using CSM.Web.Models.HRM;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace CSM.Web.Services;

public sealed class ShiftService : IShiftService
{
    private readonly IHttpClientFactory _factory;
    private readonly ProtectedSessionStorage _storage;

    public ShiftService(
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

    public async Task<IReadOnlyCollection<ShiftResponse>>
        GetAllAsync()
    {
        var client = await ClientAsync();

        return await client.GetFromJsonAsync<
            List<ShiftResponse>>("Shifts") ?? [];
    }

    public async Task<ShiftResponse?> GetByIdAsync(
        Guid shiftId)
    {
        var client = await ClientAsync();

        return await client.GetFromJsonAsync<ShiftResponse>(
            $"Shifts/{shiftId}");
    }

    public async Task<ShiftResponse> CreateAsync(
        CreateShiftRequest request)
    {
        var client = await ClientAsync();

        var response = await client.PostAsJsonAsync(
            "Shifts",
            request);

        if (!response.IsSuccessStatusCode)
        {
            var error =
                await response.Content.ReadAsStringAsync();

            throw new Exception(
                $"Shift creation failed. " +
                $"HTTP {(int)response.StatusCode} " +
                $"{response.StatusCode}: {error}");
        }

        return await response.Content
            .ReadFromJsonAsync<ShiftResponse>()
            ?? throw new Exception(
                "Failed to read shift response.");
    }

    public async Task<ShiftResponse> UpdateAsync(
        Guid shiftId,
        UpdateShiftRequest request)
    {
        var client = await ClientAsync();

        var response = await client.PutAsJsonAsync(
            $"Shifts/{shiftId}",
            request);

        if (!response.IsSuccessStatusCode)
        {
            var error =
                await response.Content.ReadAsStringAsync();

            throw new Exception(
                $"Shift update failed. " +
                $"HTTP {(int)response.StatusCode} " +
                $"{response.StatusCode}: {error}");
        }

        return await response.Content
            .ReadFromJsonAsync<ShiftResponse>()
            ?? throw new Exception(
                "Failed to read updated shift response.");
    }

    public async Task<ShiftResponse> ChangeActiveStatusAsync(
        Guid shiftId,
        ChangeShiftActiveStatusRequest request)
    {
        var client = await ClientAsync();

        var response = await client.PutAsJsonAsync(
            $"Shifts/{shiftId}/active-status",
            request);

        if (!response.IsSuccessStatusCode)
        {
            var error =
                await response.Content.ReadAsStringAsync();

            throw new Exception(
                $"Shift active status update failed. " +
                $"HTTP {(int)response.StatusCode} " +
                $"{response.StatusCode}: {error}");
        }

        return await response.Content
            .ReadFromJsonAsync<ShiftResponse>()
            ?? throw new Exception(
                "Failed to read updated shift response.");
    }
}