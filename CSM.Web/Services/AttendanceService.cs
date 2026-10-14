using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using CSM.Web.Models.HRM;

namespace CSM.Web.Services;

public sealed class AttendanceService : IAttendanceService
{
    private readonly IHttpClientFactory _factory;
    private readonly ProtectedSessionStorage _storage;

    public AttendanceService(
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

        if (token.Success && !string.IsNullOrWhiteSpace(token.Value))
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token.Value);
        }

        return client;
    }

    public async Task<IReadOnlyList<AttendanceResponse>> GetAttendancesAsync(
        Guid? employeeId = null,
        Guid? constructionSiteId = null,
        DateOnly? fromDate = null,
        DateOnly? toDate = null)
    {
        var client = await ClientAsync();

        var parameters = new List<string>();

        if (employeeId.HasValue)
        {
            parameters.Add($"employeeId={employeeId.Value}");
        }

        if (constructionSiteId.HasValue)
        {
            parameters.Add(
                $"constructionSiteId={constructionSiteId.Value}");
        }

        if (fromDate.HasValue)
        {
            parameters.Add(
                $"fromDate={fromDate.Value:yyyy-MM-dd}");
        }

        if (toDate.HasValue)
        {
            parameters.Add(
                $"toDate={toDate.Value:yyyy-MM-dd}");
        }

        var url = "Attendances";

        if (parameters.Count > 0)
        {
            url += "?" + string.Join("&", parameters);
        }

        return await client.GetFromJsonAsync<List<AttendanceResponse>>(url)
            ?? [];
    }

    public async Task<AttendanceResponse> GetAttendanceAsync(
        Guid attendanceId)
    {
        var client = await ClientAsync();

        return await client.GetFromJsonAsync<AttendanceResponse>(
            $"Attendances/{attendanceId}")
            ?? throw new InvalidOperationException(
                "Attendance record was not found.");
    }

    public async Task<AttendanceResponse> CheckInAsync(
        CheckInRequest request)
    {
        var client = await ClientAsync();

        var payload = new
        {
            request.EmployeeId,
            request.ConstructionSiteId,
            request.ShiftId,
            request.CheckInAtUtc,
            request.Latitude,
            request.Longitude,
            request.AccuracyMeters,
            Source = ParseAttendanceSource(request.Source),
            request.BiometricReference,
            request.Remarks
        };

        var response = await client.PostAsJsonAsync(
            "Attendances/check-in",
            payload);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();

            throw new Exception(
                $"Attendance check-in failed. " +
                $"HTTP {(int)response.StatusCode} " +
                $"{response.StatusCode}: {error}");
        }

        return await response.Content
            .ReadFromJsonAsync<AttendanceResponse>()
            ?? throw new Exception(
                "Failed to read attendance response.");
    }

    public async Task<AttendanceResponse> CheckOutAsync(
        Guid attendanceId,
        CheckOutRequest request)
    {
        var client = await ClientAsync();

        var response = await client.PutAsJsonAsync(
            $"Attendances/{attendanceId}/check-out",
            request);

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<AttendanceResponse>()
            ?? throw new InvalidOperationException(
                "The API returned an empty attendance response.");
    }

    public async Task<AttendanceResponse> ApproveAsync(
        Guid attendanceId,
        ApproveAttendanceRequest request)
    {
        var client = await ClientAsync();

        var response = await client.PutAsJsonAsync(
            $"Attendances/{attendanceId}/approve",
            request);

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<AttendanceResponse>()
            ?? throw new InvalidOperationException(
                "The API returned an empty attendance response.");
    }

    public async Task<AttendanceResponse> OverrideAsync(
        Guid attendanceId,
        OverrideAttendanceRequest request)
    {
        var client = await ClientAsync();

        var payload = new
        {
            request.CheckInAtUtc,
            request.CheckOutAtUtc,
            Status = ParseAttendanceStatus(request.Status),
            request.Reason,
            request.Remarks
        };

        var response = await client.PutAsJsonAsync(
            $"Attendances/{attendanceId}/override",
            payload);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();

            throw new Exception(
                $"Attendance override failed. " +
                $"HTTP {(int)response.StatusCode} " +
                $"{response.StatusCode}: {error}");
        }

        return await response.Content
            .ReadFromJsonAsync<AttendanceResponse>()
            ?? throw new Exception(
                "Failed to read attendance override response.");
    }

    private static int ParseAttendanceStatus(string value)
    {
        return value switch
        {
            "Present" => 1,
            "Absent" => 2,
            "Late" => 3,
            "EarlyDeparture" => 4,
            "Leave" => 5,
            "Holiday" => 6,
            "Weekend" => 7,
            _ => throw new ArgumentException(
                $"Invalid attendance status: {value}")
        };
    }

    private static int ParseAttendanceSource(string value)
    {
        return value switch
        {
            "MobileGps" => 1,
            "Biometric" => 2,
            "Manual" => 3,
            "Imported" => 4,
            _ => throw new ArgumentException(
                $"Invalid attendance source: {value}")
        };
    }
}