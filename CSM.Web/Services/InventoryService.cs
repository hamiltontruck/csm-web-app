using System.Net.Http.Json;
using CSM.Web.Models.Procurement;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CSM.Web.Services;

public sealed class InventoryService : IInventoryService
{
    private readonly HttpClient _http;
    private readonly IAuthService _authService;

    public InventoryService(HttpClient http, IAuthService authService)
    {
        _http = http;
        _authService = authService;
    }

    public async Task<List<CSM.Web.Models.Procurement.StockBalanceResponse>>
        GetStockBalancesAsync(
            Guid? constructionSiteId = null,
            Guid? materialId = null)
    {
        await _authService.ApplyAuthenticationAsync(_http);
        var url = "StockBalances";

        var query = new List<string>();

        if (constructionSiteId.HasValue)
            query.Add($"constructionSiteId={constructionSiteId}");

        if (materialId.HasValue)
            query.Add($"materialId={materialId}");

        if (query.Count > 0)
            url += "?" + string.Join("&", query);

        var response = await _http.GetAsync(url);

        var json = await response.Content.ReadAsStringAsync();

        Console.WriteLine($"STATUS: {response.StatusCode}");
        Console.WriteLine(json);

        response.EnsureSuccessStatusCode();

        return System.Text.Json.JsonSerializer.Deserialize<
            List<CSM.Web.Models.Procurement.StockBalanceResponse>>(
                json,
                new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? [];
    }

    public async Task<StockBalanceResponse?> GetStockBalanceAsync(
        Guid constructionSiteId,
        Guid materialId)
    {
        var balances = await GetStockBalancesAsync(
            constructionSiteId,
            materialId);

        return balances.FirstOrDefault();
    }

    public async Task<List<StockMovementResponse>> GetStockMovementsAsync(
        Guid? constructionSiteId = null,
        Guid? materialId = null)
    {
        await _authService.ApplyAuthenticationAsync(_http);

        var url = "StockMovements";

        var query = new List<string>();

        if (constructionSiteId.HasValue)
            query.Add($"constructionSiteId={constructionSiteId}");

        if (materialId.HasValue)
            query.Add($"materialId={materialId}");

        if (query.Count > 0)
            url += "?" + string.Join("&", query);

        var response = await _http.GetAsync(url);

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();

        return JsonSerializer.Deserialize<List<StockMovementResponse>>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                NumberHandling = JsonNumberHandling.AllowReadingFromString
            }) ?? [];
    }

    public async Task<StockMovementResponse?> GetStockMovementAsync(Guid id)
    {
        await _authService.ApplyAuthenticationAsync(_http);

        return await _http.GetFromJsonAsync<StockMovementResponse>(
            $"StockMovements/{id}",
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                NumberHandling = JsonNumberHandling.AllowReadingFromString
            });
    }

    public async Task<List<StockBalanceResponse>> GetStockBySiteAsync(Guid siteId)
    {
        return await GetStockBalancesAsync(constructionSiteId: siteId);
    }

    public async Task<bool> CreateStockTransferAsync(StockTransferCreateRequest request)
    {
        await _authService.ApplyAuthenticationAsync(_http);

        var response = await _http.PostAsJsonAsync("StockTransfers", request);

        var body = await response.Content.ReadAsStringAsync();

        Console.WriteLine($"STATUS: {response.StatusCode}");
        Console.WriteLine(body);

        return response.IsSuccessStatusCode;
    }

    public async Task<List<StockTransferResponse>> GetStockTransfersAsync()
    {
        await _authService.ApplyAuthenticationAsync(_http);

        return await _http.GetFromJsonAsync<List<StockTransferResponse>>(
            "StockTransfers") ?? [];
    }

    public async Task<bool> CreateStockAdjustmentAsync(
        StockAdjustmentCreateRequest request)
    {
        await _authService.ApplyAuthenticationAsync(_http);

        var response = await _http.PostAsJsonAsync(
            "StockAdjustments",
            request);

        var body = await response.Content.ReadAsStringAsync();

        Console.WriteLine($"STATUS: {response.StatusCode}");
        Console.WriteLine(body);

        return response.IsSuccessStatusCode;
    }

    public async Task<List<StockAdjustmentResponse>> GetStockAdjustmentsAsync()
    {
        await _authService.ApplyAuthenticationAsync(_http);

        return await _http.GetFromJsonAsync<List<StockAdjustmentResponse>>(
            "StockAdjustments") ?? [];
    }

    public async Task<List<StockIssueResponse>> GetStockIssuesAsync()
    {
        await _authService.ApplyAuthenticationAsync(_http);

        return await _http.GetFromJsonAsync<List<StockIssueResponse>>(
            "StockIssues") ?? [];
    }

    public async Task<bool> CreateStockIssueAsync(
        CreateStockIssueRequest request)
    {
        await _authService.ApplyAuthenticationAsync(_http);

        var response = await _http.PostAsJsonAsync(
            "StockIssues",
            request);

        var body = await response.Content.ReadAsStringAsync();

        Console.WriteLine($"STATUS: {response.StatusCode}");
        Console.WriteLine(body);

        return response.IsSuccessStatusCode;
    }

    public async Task<bool> CreateDailyMaterialUsageAsync(
        CreateDailyMaterialUsageRequest request)
    {
        await _authService.ApplyAuthenticationAsync(_http);

        var response = await _http.PostAsJsonAsync(
            "DailyMaterialUsages",
            request);

        var body = await response.Content.ReadAsStringAsync();

        Console.WriteLine($"STATUS: {response.StatusCode}");
        Console.WriteLine(body);

        return response.IsSuccessStatusCode;
    }
    public async Task<List<DailyMaterialUsageResponse>> GetDailyMaterialUsageAsync()
    {
        await _authService.ApplyAuthenticationAsync(_http);

        return await _http.GetFromJsonAsync<List<DailyMaterialUsageResponse>>(
            "DailyMaterialUsages") ?? [];
    }

    public async Task<List<StockReturnResponse>> GetStockReturnsAsync()
    {
        await _authService.ApplyAuthenticationAsync(_http);

        return await _http.GetFromJsonAsync<List<StockReturnResponse>>(
            "StockReturns") ?? [];
    }

    public async Task<bool> CreateStockReturnAsync(
        CreateStockReturnRequest request)
    {
        await _authService.ApplyAuthenticationAsync(_http);

        var response = await _http.PostAsJsonAsync(
            "StockReturns",
            request);

        return response.IsSuccessStatusCode;
    }
}