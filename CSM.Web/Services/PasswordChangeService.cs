
using System.Net.Http.Json;

namespace CSM.Web.Services;

public sealed class PasswordChangeService
{
    private readonly IHttpClientFactory _factory;
    private readonly IAuthService _authService;

    public PasswordChangeService(
        IHttpClientFactory factory,
        IAuthService authService)
    {
        _factory = factory;
        _authService = authService;
    }

    public async Task ChangePasswordAsync(
        string currentPassword,
        string newPassword,
        string confirmNewPassword,
        CancellationToken cancellationToken = default)
    {
        var client = _factory.CreateClient("CSM.Api");
        await _authService.ApplyAuthenticationAsync(client);

        var payload = new ChangePasswordInput
        {
            CurrentPassword = currentPassword,
            NewPassword = newPassword,
            ConfirmNewPassword = confirmNewPassword
        };

        using var response = await client.PostAsJsonAsync(
            "auth/change-password",
            payload,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content
                .ReadAsStringAsync(cancellationToken);

            throw new HttpRequestException(
                $"Password change failed: {(int)response.StatusCode} " +
                $"({response.StatusCode}). Response: {responseBody}",
                null,
                response.StatusCode);
        }

    }
}

public sealed class ChangePasswordInput
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmNewPassword { get; set; } = string.Empty;
}
