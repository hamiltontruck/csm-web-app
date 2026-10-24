
using System.Net.Http.Json;

namespace CSM.Web.Services;

public sealed class MyProfileService
{
    private readonly IHttpClientFactory _factory;
    private readonly IAuthService _authService;

    public MyProfileService(
        IHttpClientFactory factory,
        IAuthService authService)
    {
        _factory = factory;
        _authService = authService;
    }

    public async Task<MyProfileData> GetAsync(
        CancellationToken cancellationToken = default)
    {
        var client = _factory.CreateClient("CSM.Api");
        await _authService.ApplyAuthenticationAsync(client);

        using var response = await client.GetAsync(
            "auth/me/profile",
            cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<MyProfileData>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException(
                "The API returned an empty profile response.");
    }

    public async Task<MyProfileData> UpdateAsync(
        MyProfileData request,
        CancellationToken cancellationToken = default)
    {
        var client = _factory.CreateClient("CSM.Api");
        await _authService.ApplyAuthenticationAsync(client);

        using var response = await client.PutAsJsonAsync(
            "auth/me/profile",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<MyProfileData>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException(
                "The API returned an empty profile response.");
    }
}

public sealed class MyProfileData
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
}
