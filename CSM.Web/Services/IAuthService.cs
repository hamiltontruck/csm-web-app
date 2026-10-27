using CSM.Web.Models.Auth;

namespace CSM.Web.Services;

public interface IAuthService
{
    Guid? CompanyId { get; }

    Task<Guid?> GetCurrentCompanyIdAsync();

    Task<LoginResponse?> LoginAsync(LoginRequest request);

    Task ApplyAuthenticationAsync(HttpClient httpClient);

    Task<string?> GetTokenAsync();

    Task LogoutAsync();

    Task<IReadOnlyCollection<string>> GetRolesAsync();

    Task<IReadOnlyCollection<string>> GetPermissionsAsync();
}
