using System.Net.Http.Json;

namespace CSM.Web.Services;

public sealed class OrganizationMembershipService
{
    private readonly IHttpClientFactory _factory;
    private readonly IAuthService _authService;

    public OrganizationMembershipService(
        IHttpClientFactory factory,
        IAuthService authService)
    {
        _factory = factory;
        _authService = authService;
    }

    public async Task<IReadOnlyList<OrganizationMembershipItem>>
        GetAsync(CancellationToken cancellationToken = default)
    {
        var client = _factory.CreateClient("CSM.Api");

        await _authService.ApplyAuthenticationAsync(client);

        using var response = await client.GetAsync(
            "auth/organization-membership",
            cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<List<OrganizationMembershipItem>>(
                cancellationToken: cancellationToken)
            ?? [];
    }
}

public sealed class OrganizationMembershipItem
{
    public Guid CompanyId { get; set; }
    public Guid SiteId { get; set; }
    public string? SiteCode { get; set; }
    public string? SiteName { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Region { get; set; }
    public string? Country { get; set; }
    public string? SiteStatus { get; set; }
    public bool IsDefault { get; set; }
}