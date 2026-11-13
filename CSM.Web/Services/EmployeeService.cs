using System.Text.Json;
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

    public async Task<EmployeeCreateOptions> GetCreateOptionsAsync()
    {
        var client = await ClientAsync();
        using var response = await client.GetAsync("Employees/create-options");
        await EnsureCreateSuccessAsync(response);
        try
        {
            return await response.Content.ReadFromJsonAsync<EmployeeCreateOptions>()
                ?? throw new HttpRequestException("Unable to verify employee access. Please try again.");
        }
        catch (JsonException)
        {
            throw new HttpRequestException("Unable to verify employee access. Please try again.");
        }
    }

    public async Task<EmployeeResponse> CreateEmployeeAsync(
        CreateEmployeeRequest request)
    {
        var client = await ClientAsync();

        var payload = new
        {
            request.CompanyId,
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

        using var response = await client.PostAsJsonAsync(
            "Employees",
            payload);

        await EnsureCreateSuccessAsync(response);
        try
        {
            var created = await response.Content.ReadFromJsonAsync<EmployeeResponse>();
            if (created is null || created.Id == Guid.Empty)
                throw new HttpRequestException("Unable to confirm employee creation. Refresh the employee list before trying again.");
            return created;
        }
        catch (JsonException)
        {
            throw new HttpRequestException("Unable to confirm employee creation. Refresh the employee list before trying again.");
        }
    }

    private static async Task EnsureCreateSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        var message = response.StatusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized => "Your session is missing or has expired. Please sign in again.",
            System.Net.HttpStatusCode.Forbidden => "You do not have permission to create employees.",
            _ => "Unable to save employee. Please try again."
        };
        if (response.StatusCode is System.Net.HttpStatusCode.BadRequest or System.Net.HttpStatusCode.Conflict or System.Net.HttpStatusCode.UnprocessableEntity)
        {
            try
            {
                var body = await response.Content.ReadFromJsonAsync<JsonElement>();
                if (body.ValueKind == JsonValueKind.Object && body.TryGetProperty("message", out var value)
                    && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))
                    message = value.GetString()!;
            }
            catch (JsonException) { }
        }
        throw new HttpRequestException(message, null, response.StatusCode);
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