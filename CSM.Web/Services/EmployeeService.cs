using System.Net.Http.Headers;
using System.Net.Http.Json;
using CSM.Web.Models.HRM;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace CSM.Web.Services;

public sealed class EmployeeService : IEmployeeService
{
    private readonly IHttpClientFactory _factory;
    private readonly ProtectedSessionStorage _storage;

    public EmployeeService(
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

    public async Task<IReadOnlyList<EmployeeResponse>>
        GetEmployeesAsync(
            string? status = null,
            string? employeeScope = null)
    {
        var client = await ClientAsync();

        var query = new List<string>();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query.Add(
                $"status={Uri.EscapeDataString(status)}");
        }

        if (!string.IsNullOrWhiteSpace(employeeScope))
        {
            query.Add(
                $"employeeScope={Uri.EscapeDataString(employeeScope)}");
        }

        var url = "Employees";

        if (query.Count > 0)
        {
            url += "?" + string.Join("&", query);
        }

        return await client.GetFromJsonAsync<
            List<EmployeeResponse>>(url) ?? [];
    }

    public async Task<EmployeeResponse> GetEmployeeAsync(
        Guid employeeId)
    {
        var client = await ClientAsync();

        return await client.GetFromJsonAsync<EmployeeResponse>(
            $"Employees/{employeeId}")
            ?? throw new Exception(
                "Failed to read employee details.");
    }

    public async Task<EmployeeResponse> CreateEmployeeAsync(
        CreateEmployeeRequest request)
    {
        var client = await ClientAsync();

        var payload = new
        {
            request.EmployeeNumber,
            request.FirstName,
            request.MiddleName,
            request.LastName,
            request.PhoneNumber,
            request.Email,
            request.NationalIdNumber,
            request.TaxIdentificationNumber,
            request.DateOfBirth,
            request.HireDate,
            request.JobTitle,
            request.Department,
            EmployeeType = ParseEmployeeType(request.EmployeeType),
            EmployeeScope = request.EmployeeScope,
            WageType = ParseWageType(request.WageType),
            request.BaseWage,
            request.CurrencyCode,
            request.BankName,
            request.BankAccountNumber,
            request.EmergencyContactName,
            request.EmergencyContactPhone
        };

        var response = await client.PostAsJsonAsync(
            "Employees",
            payload);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();

            throw new Exception(
                $"Employee creation failed. " +
                $"HTTP {(int)response.StatusCode} " +
                $"{response.StatusCode}: {error}");
        }

        return await response.Content
            .ReadFromJsonAsync<EmployeeResponse>()
            ?? throw new Exception(
                "Failed to read employee response.");
    }

    public async Task<EmployeeResponse> UpdateEmployeeAsync(
        Guid employeeId,
        UpdateEmployeeRequest request)
    {
        var client = await ClientAsync();

        var payload = new
        {
            request.FirstName,
            request.MiddleName,
            request.LastName,
            request.PhoneNumber,
            request.Email,
            request.NationalIdNumber,
            request.TaxIdentificationNumber,
            request.DateOfBirth,
            request.HireDate,
            request.JobTitle,
            request.Department,
            EmployeeType = ParseEmployeeType(request.EmployeeType),
            EmployeeScope = request.EmployeeScope,
            WageType = ParseWageType(request.WageType),
            request.BaseWage,
            request.CurrencyCode,
            request.BankName,
            request.BankAccountNumber,
            request.EmergencyContactName,
            request.EmergencyContactPhone
        };

        var response = await client.PutAsJsonAsync(
            $"Employees/{employeeId}",
            payload);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();

            throw new Exception(
                $"Employee update failed. " +
                $"HTTP {(int)response.StatusCode} " +
                $"{response.StatusCode}: {error}");
        }

        return await response.Content
            .ReadFromJsonAsync<EmployeeResponse>()
            ?? throw new Exception(
                "Failed to read updated employee response.");
    }

    public async Task<EmployeeResponse> ChangeEmployeeStatusAsync(
        Guid employeeId,
        ChangeEmployeeStatusRequest request)
    {
        var client = await ClientAsync();

        var status = request.Status switch
        {
            "Active" => 1,
            "OnLeave" => 2,
            "Suspended" => 3,
            "Terminated" => 4,
            "Inactive" => 5,
            _ => throw new ArgumentException(
                $"Invalid employee status: {request.Status}")
        };

        var payload = new
        {
            Status = status,
            request.TerminationDate
        };

        var response = await client.PutAsJsonAsync(
            $"Employees/{employeeId}/status",
            payload);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();

            throw new Exception(
                $"Employee status update failed. " +
                $"HTTP {(int)response.StatusCode} " +
                $"{response.StatusCode}: {error}");
        }

        return await response.Content
            .ReadFromJsonAsync<EmployeeResponse>()
            ?? throw new Exception(
                "Failed to read updated employee response.");
    }

    private static int ParseEmployeeType(string value)
    {
        return value switch
        {
            "Permanent" => 1,
            "Contract" => 2,
            "DailyWage" => 3,
            "Hourly" => 4,
            "SubcontractorCrew" => 5,
            _ => throw new ArgumentException(
                $"Invalid employee type: {value}")
        };
    }

    private static int ParseWageType(string value)
    {
        return value switch
        {
            "Monthly" => 1,
            "Daily" => 2,
            "Hourly" => 3,
            _ => throw new ArgumentException(
                $"Invalid wage type: {value}")
        };
    }

}