namespace CSM.Web.Models.Auth;

public class LoginResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;

    public DateTime AccessTokenExpiresAtUtc { get; set; }

    public Guid UserId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid? EmployeeId { get; set; }

    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;

    public bool MustChangePassword { get; set; }

    public List<string> Roles { get; set; } = [];
}