using System.Net.Http.Json;
using CSM.Web.Models.Procurement;

namespace CSM.Web.Services;

public class VendorService : IVendorService
{
    private readonly HttpClient _http;
    private readonly AuthService _authService;

    public VendorService(
        IHttpClientFactory factory,
        AuthService authService)
    {
        _http = factory.CreateClient("CSM.Api");
        _authService = authService;
    }

    public async Task<List<VendorResponse>> GetVendorsAsync()
    {
        await _authService.ApplyAuthenticationAsync(_http);

        return await _http.GetFromJsonAsync<List<VendorResponse>>(
            "Vendors") ?? [];
    }

    public async Task<VendorResponse?> GetVendorAsync(Guid id)
    {
        await _authService.ApplyAuthenticationAsync(_http);

        return await _http.GetFromJsonAsync<VendorResponse>(
            $"Vendors/{id}");
    }

    public async Task<bool> CreateVendorAsync(CreateVendorRequest request)
    {
        await _authService.ApplyAuthenticationAsync(_http);

        var response = await _http.PostAsJsonAsync("Vendors", request);

        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateVendorAsync(
        Guid id,
        CreateVendorRequest request)
    {
        await _authService.ApplyAuthenticationAsync(_http);

        var response = await _http.PutAsJsonAsync(
            $"Vendors/{id}",
            request);

        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteVendorAsync(Guid id)
    {
        await _authService.ApplyAuthenticationAsync(_http);

        var response = await _http.DeleteAsync(
            $"Vendors/{id}");

        return response.IsSuccessStatusCode;
    }
}