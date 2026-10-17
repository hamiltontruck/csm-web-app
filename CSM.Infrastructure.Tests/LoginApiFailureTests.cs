using System.Net;
using CSM.Web.Models.Auth;
using CSM.Web.Services;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class LoginApiFailureTests
{
    [Theory]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Server_failure_is_reported_as_service_unavailable(HttpStatusCode status)
    {
        var service = CreateService(status);

        var error = await Assert.ThrowsAsync<HttpRequestException>(
            () => service.LoginAsync(new LoginRequest()));

        Assert.Equal(status, error.StatusCode);
        Assert.Contains("temporarily unavailable", error.Message);
    }

    [Fact]
    public async Task Unauthorized_response_is_reported_as_invalid_credentials()
    {
        var service = CreateService(HttpStatusCode.Unauthorized);

        Assert.Null(await service.LoginAsync(new LoginRequest()));
    }

    private static AuthService CreateService(HttpStatusCode status)
    {
        var factory = new ClientFactory(new HttpClient(new ResponseHandler(status))
        {
            BaseAddress = new Uri("https://api.example.test/api/")
        });
        // Failed login responses must return before reading or writing browser storage.
        return new AuthService(factory, null!);
    }

    private sealed class ClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class ResponseHandler(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(status));
    }
}
