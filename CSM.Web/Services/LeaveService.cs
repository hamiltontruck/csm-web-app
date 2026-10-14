using System.Net.Http.Headers;
using System.Net.Http.Json;
using CSM.Web.Models.HRM;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace CSM.Web.Services;

public sealed class LeaveService : ILeaveService
{
    private readonly IHttpClientFactory _factory;
    private readonly ProtectedSessionStorage _storage;

    public LeaveService(
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

    public async Task<IReadOnlyCollection<LeaveResponse>>
        GetAllAsync(
            Guid? employeeId = null,
            DateOnly? fromDate = null,
            DateOnly? toDate = null)
    {
        var client = await ClientAsync();

        var query = new List<string>();

        if (employeeId.HasValue)
            query.Add($"employeeId={employeeId.Value}");

        if (fromDate.HasValue)
            query.Add($"fromDate={fromDate.Value:yyyy-MM-dd}");

        if (toDate.HasValue)
            query.Add($"toDate={toDate.Value:yyyy-MM-dd}");

        var url = "Leaves";

        if (query.Count > 0)
            url += "?" + string.Join("&", query);

        return await client.GetFromJsonAsync<
            List<LeaveResponse>>(url) ?? [];
    }

    public async Task<LeaveResponse?> GetByIdAsync(
        Guid leaveRequestId)
    {
        var client = await ClientAsync();

        return await client.GetFromJsonAsync<LeaveResponse>(
            $"Leaves/{leaveRequestId}");
    }

    public async Task<LeaveResponse> CreateAsync(
        CreateLeaveRequest request)
    {
        var client = await ClientAsync();

        var response = await client.PostAsJsonAsync(
            "Leaves",
            request);

        if (!response.IsSuccessStatusCode)
        {
            var error =
                await response.Content.ReadAsStringAsync();

            throw new Exception(
                $"Leave creation failed. " +
                $"HTTP {(int)response.StatusCode} " +
                $"{response.StatusCode}: {error}");
        }

        return await response.Content
            .ReadFromJsonAsync<LeaveResponse>()
            ?? throw new Exception(
                "Failed to read leave response.");
    }

    public async Task<LeaveResponse> ReviewAsync(
        Guid leaveRequestId,
        ReviewLeaveRequest request)
    {
        var client = await ClientAsync();

        var response = await client.PutAsJsonAsync(
            $"Leaves/{leaveRequestId}/review",
            request);

        if (!response.IsSuccessStatusCode)
        {
            var error =
                await response.Content.ReadAsStringAsync();

            throw new Exception(
                $"Leave review failed. " +
                $"HTTP {(int)response.StatusCode} " +
                $"{response.StatusCode}: {error}");
        }

        return await response.Content
            .ReadFromJsonAsync<LeaveResponse>()
            ?? throw new Exception(
                "Failed to read reviewed leave response.");
    }

    public async Task<LeaveResponse> CancelAsync(
        Guid leaveRequestId,
        CancelLeaveRequest request)
    {
        var client = await ClientAsync();

        var response = await client.PutAsJsonAsync(
            $"Leaves/{leaveRequestId}/cancel",
            request);

        if (!response.IsSuccessStatusCode)
        {
            var error =
                await response.Content.ReadAsStringAsync();

            throw new Exception(
                $"Leave cancellation failed. " +
                $"HTTP {(int)response.StatusCode} " +
                $"{response.StatusCode}: {error}");
        }

        return await response.Content
            .ReadFromJsonAsync<LeaveResponse>()
            ?? throw new Exception(
                "Failed to read cancelled leave response.");
    }
}