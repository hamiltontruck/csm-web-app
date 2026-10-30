using System.Net;
using System.Reflection;
using Bunit;
using CSM.Web.Components.Pages;
using CSM.Web.Models.HRM;
using CSM.Web.Services;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class HrDashboardFeedbackTests
{
    [Theory]
    [InlineData(401, "Your session is missing or has expired. Please sign in again.")]
    [InlineData(403, "You do not have permission to view contractor summaries.")]
    [InlineData(500, "Unable to load contractor summary. Please try again.")]
    public void Contractor_failure_does_not_crash_HR_or_show_false_counts(int status, string expected)
    {
        using var context = Context(out var api);
        api.Status = (HttpStatusCode)status;
        var page = context.RenderComponent<Hr>();
        page.WaitForAssertion(() => Assert.Equal(expected, page.Find("[role=alert]").TextContent.Trim()));
        Assert.Equal("—", page.Find("#hr-contractor-count").TextContent.Trim());
        Assert.DoesNotContain("private diagnostic", page.Markup);
        Assert.DoesNotContain("Manage Contractors", page.Markup);
        Assert.DoesNotContain("<h2>0", page.Markup);
        Assert.Equal(status == 401, page.FindAll("#hr-sign-in").Count == 1);
        Assert.Equal(status == 500, page.FindAll("#retry-hr-summary").Count == 1);
        Assert.Contains("Manage Attendances", page.Markup);
        if (status == 401)
            Assert.Equal("/login?returnUrl=%2Fhr", page.Find("#hr-sign-in").GetAttribute("href"));
    }

    [Fact]
    public void Unexpected_failure_has_safe_retry_feedback()
    {
        using var context = Context(out var api);
        api.Unexpected = true;
        var page = context.RenderComponent<Hr>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("#retry-hr-summary")));
        Assert.DoesNotContain("private diagnostic", page.Markup);
        Assert.Equal("—", page.Find("#hr-contractor-count").TextContent.Trim());
    }

    [Fact]
    public void Retry_restores_the_real_contractor_count_and_navigation()
    {
        using var context = Context(out var api);
        api.Status = HttpStatusCode.ServiceUnavailable;
        var page = context.RenderComponent<Hr>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("#retry-hr-summary")));
        api.Status = null;
        api.Count = 2;
        page.Find("#retry-hr-summary").Click();
        page.WaitForAssertion(() => Assert.Equal("2", page.Find("#hr-contractor-count").TextContent.Trim()));
        Assert.Empty(page.FindAll("[role=alert]"));
        Assert.Contains("Manage Contractors", page.Markup);
    }

    [Fact]
    public void Successful_empty_response_can_display_zero_contractors()
    {
        using var context = Context(out _);
        var page = context.RenderComponent<Hr>();
        page.WaitForAssertion(() => Assert.Equal("0", page.Find("#hr-contractor-count").TextContent.Trim()));
        Assert.Empty(page.FindAll("[role=alert]"));
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(500)]
    public async Task Contractor_client_preserves_HTTP_status_without_exposing_response_body(int status)
    {
        using var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddDataProtection();
        context.Services.AddScoped<ProtectedSessionStorage>();
        using var http = new HttpClient(new Response((HttpStatusCode)status)) { BaseAddress = new Uri("https://api.test/api/") };
        var service = new ContractorService(new Factory(http), context.Services.GetRequiredService<ProtectedSessionStorage>());
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => service.GetContractorsAsync());
        Assert.Equal((HttpStatusCode)status, error.StatusCode);
        Assert.DoesNotContain("private diagnostic", error.Message);
    }

    private static TestContext Context(out Api api)
    {
        var context = new TestContext();
        var service = DispatchProxy.Create<IContractorService, Api>();
        api = (Api)(object)service;
        context.Services.AddSingleton(service);
        context.Services.AddSingleton(DispatchProxy.Create<IAuthService, Auth>());
        return context;
    }
    public class Api : DispatchProxy
    {
        public HttpStatusCode? Status { get; set; }
        public bool Unexpected { get; set; }
        public int Count { get; set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name != nameof(IContractorService.GetContractorsAsync))
                throw new InvalidOperationException("Unexpected mutation.");
            if (Unexpected) return Task.FromException<IReadOnlyList<ContractorResponse>>(new InvalidOperationException("private diagnostic"));
            if (Status.HasValue) return Task.FromException<IReadOnlyList<ContractorResponse>>(new HttpRequestException("private diagnostic", null, Status));
            return Task.FromResult<IReadOnlyList<ContractorResponse>>(Enumerable.Range(0, Count).Select(_ => new ContractorResponse()).ToList());
        }
    }
    public class Auth : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            Task.FromResult<IReadOnlyCollection<string>>(method!.Name == nameof(IAuthService.GetRolesAsync)
                ? new[] { "SuperAdmin", "HrOfficer" }
                : new[] { "HRM_Contractor_View", "HRM_Attendance_View" });
    }
    private sealed class Factory(HttpClient http) : IHttpClientFactory { public HttpClient CreateClient(string name) => http; }
    private sealed class Response(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent("private diagnostic") });
    }
}
