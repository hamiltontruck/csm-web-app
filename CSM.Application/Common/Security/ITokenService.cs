using CSM.Domain.Entities.Identity;

namespace CSM.Application.Common.Security;

public interface ITokenService
{
    string GenerateAccessToken(
        User user,
        IReadOnlyCollection<string> roles);

    string GenerateRefreshToken();

    string HashRefreshToken(string refreshToken);
}