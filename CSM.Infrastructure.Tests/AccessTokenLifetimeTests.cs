using System.IdentityModel.Tokens.Jwt;
using CSM.Application.Common.Security;
using CSM.Domain.Entities.Identity;
using CSM.Infrastructure.Security;
using Microsoft.Extensions.Options;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class AccessTokenLifetimeTests
{
    [Theory]
    [InlineData(null, 60)]
    [InlineData(30, 30)]
    public void Token_expiry_uses_one_hour_default_and_respects_explicit_configuration(int? configured, int expected)
    {
        var settings = new JwtSettings
        {
            Issuer = "test", Audience = "test",
            SecretKey = "test-only-signing-key-with-at-least-32-characters"
        };
        if (configured.HasValue) settings.AccessTokenMinutes = configured.Value;
        var started = DateTime.UtcNow;
        var raw = new TokenService(Options.Create(settings)).GenerateAccessToken(
            new User { Id = Guid.NewGuid(), Email = "test@example.com", FirstName = "Test", LastName = "User" }, []);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(raw);
        Assert.InRange(token.ValidTo, started.AddMinutes(expected).AddSeconds(-2),
            DateTime.UtcNow.AddMinutes(expected).AddSeconds(2));
    }
}
