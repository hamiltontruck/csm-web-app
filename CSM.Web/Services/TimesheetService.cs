using System.Net.Http.Headers;
using System.Net.Http.Json;
using CSM.Web.Models.HRM;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace CSM.Web.Services;

public sealed class TimesheetService : ITimesheetService
{
    private readonly IHttpClientFactory _factory;
    private readonly ProtectedSessionStorage _storage;

    public TimesheetService(
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

    public async Task<IReadOnlyCollection<TimesheetResponse>>
        GetAllAsync(
            Guid? employeeId = null,
            DateOnly? fromDate = null,
            DateOnly? toDate = null)
    {
        var client = await ClientAsync();

        var query = new List<string>();

        if (employeeId.HasValue)
        {
            query.Add($"employeeId={employeeId.Value}");
        }

        if (fromDate.HasValue)
        {
            query.Add($"fromDate={fromDate.Value:yyyy-MM-dd}");
        }

        if (toDate.HasValue)
        {
            query.Add($"toDate={toDate.Value:yyyy-MM-dd}");
        }

        var url = "Timesheets";

        if (query.Count > 0)
        {
            url += "?" + string.Join("&", query);
        }

        return await client.GetFromJsonAsync<
            List<TimesheetResponse>>(url) ?? [];
    }

    public async Task<TimesheetResponse?> GetByIdAsync(
        Guid timesheetId)
    {
        var client = await ClientAsync();

        return await client.GetFromJsonAsync<TimesheetResponse>(
            $"Timesheets/{timesheetId}");
    }

    public async Task<TimesheetResponse> CreateAsync(
        CreateTimesheetRequest request)
    {
        var client = await ClientAsync();

        var response = await client.PostAsJsonAsync(
            "Timesheets",
            request);

        if (!response.IsSuccessStatusCode)
        {
            var error =
                await response.Content.ReadAsStringAsync();

            throw new Exception(
                $"Timesheet creation failed. " +
                $"HTTP {(int)response.StatusCode} " +
                $"{response.StatusCode}: {error}");
        }

        return await response.Content
            .ReadFromJsonAsync<TimesheetResponse>()
            ?? throw new Exception(
                "Failed to read timesheet response.");
    }

    public async Task<TimesheetResponse> SubmitAsync(
        Guid timesheetId,
        SubmitTimesheetRequest request)
    {
        var client = await ClientAsync();

        using var response = await client.PutAsJsonAsync(
            $"Timesheets/{timesheetId}/submit",
            request);

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<TimesheetResponse>()
            ?? throw new Exception(
                "Failed to read submitted timesheet response.");
    }

    public async Task<TimesheetResponse> ReviewAsync(
        Guid timesheetId,
        ReviewTimesheetRequest request)
    {
        var client = await ClientAsync();

        using var response = await client.PutAsJsonAsync(
            $"Timesheets/{timesheetId}/review",
            request);

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<TimesheetResponse>()
            ?? throw new Exception(
                "Failed to read reviewed timesheet response.");
    }

    public async Task<TimesheetResponse> CancelAsync(
        Guid timesheetId,
        CancelTimesheetRequest request)
    {
        var client = await ClientAsync();

        using var response = await client.PutAsJsonAsync(
            $"Timesheets/{timesheetId}/cancel",
            request);

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<TimesheetResponse>()
            ?? throw new Exception(
                "Failed to read cancelled timesheet response.");
    }
}