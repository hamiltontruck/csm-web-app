
using System.Net.Http.Json;

namespace CSM.Web.Services;

public sealed class PrivacyDataRequestService
{
    private readonly IHttpClientFactory _factory;
    private readonly IAuthService _authService;

    public PrivacyDataRequestService(
        IHttpClientFactory factory,
        IAuthService authService)
    {
        _factory = factory;
        _authService = authService;
    }

    public async Task<List<PrivacyDataRequestItem>> GetMineAsync(
        CancellationToken cancellationToken = default)
    {
        var client = _factory.CreateClient("CSM.Api");
        await _authService.ApplyAuthenticationAsync(client);

        using var response = await client.GetAsync(
            "privacy-data-requests/mine",
            cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<List<PrivacyDataRequestItem>>(
                cancellationToken: cancellationToken)
            ?? [];
    }

    public async Task SubmitAsync(
        string requestType,
        string? description,
        CancellationToken cancellationToken = default)
    {
        var client = _factory.CreateClient("CSM.Api");
        await _authService.ApplyAuthenticationAsync(client);

        using var response = await client.PostAsJsonAsync(
            "privacy-data-requests",
            new CreatePrivacyDataRequestInput
            {
                RequestType = requestType,
                Description = description
            },
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(
                cancellationToken);

            throw new HttpRequestException(
                $"Privacy request submission failed: " +
                $"{(int)response.StatusCode} ({response.StatusCode}). " +
                $"Response: {responseBody}",
                null,
                response.StatusCode);
        }
    }
}

public sealed class PrivacyDataRequestItem
{
    public Guid Id { get; set; }
    public string RequestType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime RequestedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? ResolutionNotes { get; set; }
}

public sealed class CreatePrivacyDataRequestInput
{
    public string RequestType { get; set; } = string.Empty;

    public string? Description { get; set; }
}
