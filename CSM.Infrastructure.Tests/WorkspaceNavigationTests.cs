using System.Net;
using System.Reflection;
using Bunit;
using CSM.Web.Components.Layout;
using CSM.Web.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class WorkspaceNavigationTests
{
    [Fact]
    public void Tapping_a_section_heading_does_not_dismiss_navigation()
    {
        using var context = Context();
        var menu = context.RenderComponent<NavMenu>();
        menu.Find(".navbar-toggler").Click();
        // bUnit reports a missing handler when this inert heading has no bubbling
        // click listener. A real browser simply leaves the menu unchanged.
        try { menu.Find(".section-title").Click(); }
        catch (MissingEventHandlerException) { }
        Assert.Equal("true", menu.Find(".navbar-toggler").GetAttribute("aria-expanded"));
    }

    [Fact]
    public void Escape_on_the_toggle_closes_navigation_and_restores_focus()
    {
        using var context = Context();
        var menu = context.RenderComponent<NavMenu>();
        menu.Find(".navbar-toggler").Click();
        Assert.Equal("true", menu.Find(".navbar-toggler").GetAttribute("aria-expanded"));

        menu.Find(".navbar-toggler").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Equal("false", menu.Find(".navbar-toggler").GetAttribute("aria-expanded"));
        Assert.DoesNotContain("is-open", menu.Find("#workspace-navigation").ClassList);
        Assert.Contains(context.JSInterop.Invocations, call => call.Identifier == "Blazor._internal.domWrapper.focus");
    }

    [Fact]
    public void Selecting_a_route_closes_navigation()
    {
        using var context = Context();
        var menu = context.RenderComponent<NavMenu>();
        menu.Find(".navbar-toggler").Click();
        menu.Find("a[href='/settings']").Click();
        Assert.Equal("false", menu.Find(".navbar-toggler").GetAttribute("aria-expanded"));
    }

    [Fact]
    public void Escape_inside_navigation_closes_it_and_sign_out_remains_inside_the_scroll_area()
    {
        using var context = Context();
        var menu = context.RenderComponent<NavMenu>();
        Assert.Single(menu.FindAll("#workspace-navigation .signout button"));
        menu.Find(".navbar-toggler").Click();
        menu.Find("#workspace-navigation").KeyDown(new KeyboardEventArgs { Key = "Escape" });
        Assert.Equal("false", menu.Find(".navbar-toggler").GetAttribute("aria-expanded"));
    }

    private static TestContext Context()
    {
        var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddSingleton(DispatchProxy.Create<IAuthService, AuthProxy>());
        context.Services.AddSingleton(DispatchProxy.Create<INotificationApiService, NotificationProxy>());
        context.Services.AddSingleton(new HttpClient(new IdentityHandler()) { BaseAddress = new Uri("https://test.invalid/api/") });
        context.Services.AddScoped<CompanyWebService>();
        return context;
    }

    public class AuthProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch
        {
            nameof(IAuthService.GetPermissionsAsync) => Task.FromResult<IReadOnlyCollection<string>>([]),
            nameof(IAuthService.GetTokenAsync) => Task.FromResult<string?>("test-token"),
            _ => throw new InvalidOperationException($"Unexpected auth operation: {method.Name}")
        };
    }

    public class NotificationProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            method!.Name == nameof(INotificationApiService.GetUnreadCountAsync)
                ? Task.FromResult(0) : throw new InvalidOperationException(method.Name);
    }

    private sealed class IdentityHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"roles\":[]}") });
    }
}
