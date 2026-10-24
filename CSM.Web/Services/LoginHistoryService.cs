
using System.Net.Http.Json;

namespace CSM.Web.Services;

public sealed class LoginHistoryService
{
    private readonly IHttpClientFactory _factory;
    private readonly IAuthService _authService;

    public LoginHistoryService(
        IHttpClientFactory factory,
        IAuthService authService)
    {
        _factory = factory;
        _authService = authService;
    }

    public async Task<IReadOnlyList<LoginHistoryItem>> GetHistoryAsync(
        CancellationToken cancellationToken = default)
    {
        var client = _factory.CreateClient("CSM.Api");

        await _authService.ApplyAuthenticationAsync(client);

        using var response = await client.GetAsync(
            "auth/login-history",
            cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content
                   .ReadFromJsonAsync<List<LoginHistoryItem>>(
                       cancellationToken: cancellationToken)
               ?? [];
    }
}

public sealed class LoginHistoryItem
{
    public Guid Id { get; set; }

    public DateTime OccurredAtUtc { get; set; }

    public bool Succeeded { get; set; }

    public string? IpAddress { get; set; }

    public string? DeviceName { get; set; }

    public string? FailureReason { get; set; }
}
