using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using Bunit;
using CSM.Web.Components.Pages;
using CSM.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class UserListFeedbackTests
{
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "Please sign in again.")]
    [InlineData(HttpStatusCode.Forbidden, "You do not have permission to view users.")]
    [InlineData(HttpStatusCode.InternalServerError, "The user service is temporarily unavailable.")]
    public void Failed_list_hides_management_controls_and_shows_safe_status(HttpStatusCode status, string message)
    {
        using var api = new Api { Status = status };
        using var context = Context(api);
        var page = context.RenderComponent<Users>();
        page.WaitForAssertion(() => Assert.Contains(message, page.Find("[role=alert]").TextContent));
        Assert.Empty(page.FindAll("table"));
        Assert.DoesNotContain("+ New User", page.Markup);
        Assert.DoesNotContain("HttpRequestException", page.Markup);
        Assert.DoesNotContain("/src/", page.Markup);
        if (status == HttpStatusCode.Unauthorized)
            Assert.Equal("/login", page.Find("#users-sign-in").GetAttribute("href"));
        if (status == HttpStatusCode.Forbidden)
            Assert.Empty(page.FindAll("#retry-users"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Network_or_timeout_failure_is_safe_and_retry_recovers(bool timeout)
    {
        using var api = new Api { Fail = true, Timeout = timeout };
        using var context = Context(api);
        var page = context.RenderComponent<Users>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("[role=alert]")));
        Assert.DoesNotContain("private diagnostic", page.Markup);
        Assert.Empty(page.FindAll("table"));
        Assert.DoesNotContain("+ New User", page.Markup);
        api.Fail = false;
        page.Find("#retry-users").Click();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("table")));
        Assert.Empty(page.FindAll("[role=alert]"));
        Assert.Contains("+ New User", page.Markup);
    }

    private static TestContext Context(Api handler)
    {
        var context = new TestContext();
        var auth = DispatchProxy.Create<IAuthService, Auth>();
        context.Services.AddSingleton(new UserWebService(
            new HttpClient(handler, false) { BaseAddress = new Uri("https://api.test/api/") }, auth));
        return context;
    }

    public class Auth : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            method!.Name == "GetTokenAsync" ? Task.FromResult<string?>("test-token") :
            throw new NotSupportedException(method.Name);
    }

    private sealed class Api : HttpMessageHandler
    {
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        public bool Fail { get; set; }
        public bool Timeout { get; set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Fail)
                return Task.FromException<HttpResponseMessage>(Timeout ?
                    new TaskCanceledException("private diagnostic") : new HttpRequestException("private diagnostic"));
            return Task.FromResult(new HttpResponseMessage(Status) {
                Content = Status == HttpStatusCode.OK ? JsonContent.Create(Array.Empty<object>()) :
                    new StringContent("private diagnostic") });
        }
    }
}
