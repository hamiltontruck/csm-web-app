
using System.Net.Http.Json;

namespace CSM.Web.Services;

public sealed class CurrentUserProfileService
{
    private readonly IHttpClientFactory _factory;
    private readonly IAuthService _authService;

    public CurrentUserProfileService(
        IHttpClientFactory factory,
        IAuthService authService)
    {
        _factory = factory;
        _authService = authService;
    }

    public async Task<CurrentUserProfile?> GetAsync(
        CancellationToken cancellationToken = default)
    {
        var client = _factory.CreateClient("CSM.Api");

        await _authService.ApplyAuthenticationAsync(client);

        using var response = await client.GetAsync(
            "auth/me",
            cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<CurrentUserProfile>(
                cancellationToken: cancellationToken);
    }
}

public sealed class CurrentUserProfile
{
    public string? UserId { get; set; }

    public string? Email { get; set; }

    public string? Name { get; set; }

    public string? CompanyId { get; set; }

    public string? EmployeeId { get; set; }

    public List<string> Roles { get; set; } = [];

    public bool HasActiveSiteAccess { get; set; }
}
