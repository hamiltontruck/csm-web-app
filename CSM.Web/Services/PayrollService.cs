using System.Net.Http.Headers;
using System.Net.Http.Json;
using CSM.Web.Models.HRM;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace CSM.Web.Services;

public sealed class PayrollService : IPayrollService
{
    private readonly IHttpClientFactory _factory;
    private readonly ProtectedSessionStorage _storage;

    public PayrollService(
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

    public async Task<IReadOnlyCollection<PayrollResponse>> GetAllAsync(
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

        var url = "Payroll";

        if (query.Count > 0)
            url += "?" + string.Join("&", query);

        using var response = await client.GetAsync(url);

        response.EnsureSuccessStatusCode();

        return await response.Content
                   .ReadFromJsonAsync<IReadOnlyCollection<PayrollResponse>>()
               ?? [];
    }

    public async Task<PayrollResponse?> GetByIdAsync(
        Guid payrollRecordId)
    {
        var client = await ClientAsync();

        using var response =
            await client.GetAsync($"Payroll/{payrollRecordId}");

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<PayrollResponse>();
    }

    public async Task<PayrollResponse> CreateAsync(
        CreatePayrollRequest request)
    {
        var client = await ClientAsync();

        using var response =
            await client.PostAsJsonAsync("Payroll", request);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"Failed to create payroll record. " +
                $"Status: {(int)response.StatusCode}. {error}");
        }

        return await response.Content
                   .ReadFromJsonAsync<PayrollResponse>()
               ?? throw new InvalidOperationException(
                   "The API returned an empty payroll response.");
    }

    public async Task<PayrollResponse> CalculateAsync(
        Guid payrollRecordId,
        CalculatePayrollRequest request)
    {
        var client = await ClientAsync();

        using var response =
            await client.PutAsJsonAsync(
                $"Payroll/{payrollRecordId}/calculate",
                request);

        response.EnsureSuccessStatusCode();

        return await response.Content
                   .ReadFromJsonAsync<PayrollResponse>()
               ?? throw new InvalidOperationException(
                   "The API returned an empty payroll response.");
    }

    public async Task<PayrollResponse> ApproveAsync(
        Guid payrollRecordId,
        ApprovePayrollRequest request)
    {
        var client = await ClientAsync();

        using var response =
            await client.PutAsJsonAsync(
                $"Payroll/{payrollRecordId}/approve",
                request);

        response.EnsureSuccessStatusCode();

        return await response.Content
                   .ReadFromJsonAsync<PayrollResponse>()
               ?? throw new InvalidOperationException(
                   "The API returned an empty payroll response.");
    }

    public async Task<PayrollResponse> MarkPaidAsync(
        Guid payrollRecordId,
        MarkPayrollPaidRequest request)
    {
        var client = await ClientAsync();

        using var response =
            await client.PutAsJsonAsync(
                $"Payroll/{payrollRecordId}/paid",
                request);

        response.EnsureSuccessStatusCode();

        return await response.Content
                   .ReadFromJsonAsync<PayrollResponse>()
               ?? throw new InvalidOperationException(
                   "The API returned an empty payroll response.");
    }

    public async Task<PayrollResponse> CancelAsync(
        Guid payrollRecordId,
        CancelPayrollRequest request)
    {
        var client = await ClientAsync();

        using var response =
            await client.PutAsJsonAsync(
                $"Payroll/{payrollRecordId}/cancel",
                request);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"Failed to cancel payroll. " +
                $"Status: {(int)response.StatusCode}. {error}");
        }

        return await response.Content
                   .ReadFromJsonAsync<PayrollResponse>()
               ?? throw new InvalidOperationException(
                   "The API returned an empty payroll response.");
    }

    public async Task<PayrollResponse> AddAdjustmentAsync(
        Guid payrollRecordId,
        CreatePayrollAdjustmentRequest request)
    {
        var client = await ClientAsync();

        using var response =
            await client.PostAsJsonAsync(
                $"Payroll/{payrollRecordId}/adjustments",
                request);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"Failed to add payroll adjustment. " +
                $"Status: {(int)response.StatusCode}. {error}");
        }

        return await response.Content
                   .ReadFromJsonAsync<PayrollResponse>()
               ?? throw new InvalidOperationException(
                   "The API returned an empty payroll response.");
    }

    public async Task<byte[]> GeneratePayslipAsync(
        Guid payrollRecordId)
    {
        var client = await ClientAsync();

        using var response =
            await client.GetAsync(
                $"Payroll/{payrollRecordId}/payslip");

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsByteArrayAsync();
    }
}