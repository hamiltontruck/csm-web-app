using System.Text.Json;
using CSM.Application.Auth.Dtos;
using CSM.Web.Models.Auth;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class LoginResponseContractTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("ab609528-72d0-4010-b4e8-9f690fa31671")]
    public void Api_login_response_deserializes_for_users_with_or_without_a_company(string? companyId)
    {
        Guid? expectedCompanyId = companyId is null ? null : Guid.Parse(companyId);
        var response = new AuthResponse(
            "test-access-token", "test-refresh-token", DateTime.UtcNow.AddMinutes(15),
            Guid.NewGuid(), expectedCompanyId, null, "user@example.test",
            "Test User", false, ["SuperAdmin"]);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var json = JsonSerializer.Serialize(response, options);

        var actual = JsonSerializer.Deserialize<LoginResponse>(json, options);

        Assert.NotNull(actual);
        Assert.Equal(expectedCompanyId, actual.CompanyId);
        Assert.Equal(response.AccessToken, actual.AccessToken);
        Assert.Equal(response.UserId, actual.UserId);
    }
}
