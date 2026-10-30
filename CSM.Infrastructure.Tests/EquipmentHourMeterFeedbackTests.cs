using System.Net;
using System.Reflection;
using Bunit;
using CSM.Web.Components.Pages;
using CSM.Web.Models.Equipment;
using CSM.Web.Models.Sites;
using CSM.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class EquipmentHourMeterFeedbackTests
{
    private static readonly Guid SiteId = Guid.Parse("ae408187-2e4a-41b2-85d9-c4ae6f9e4f66");
    private static readonly Guid Active = Guid.NewGuid();
    private static readonly Guid Other = Guid.NewGuid();
    private static readonly Guid Released = Guid.NewGuid();

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(500)]
    public async Task Hour_client_preserves_status_without_API_body(int status)
    {
        using var http = new HttpClient(new Response((HttpStatusCode)status)) { BaseAddress = new Uri("https://api.test/api/") };
        var service = new EquipmentService(new Factory(http), DispatchProxy.Create<IAuthService, Auth>());
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => service.GetHourLogsAsync());
        Assert.Equal((HttpStatusCode)status, error.StatusCode);
        Assert.DoesNotContain("private diagnostic", error.Message);
    }

    [Theory]
    [InlineData(false, 401)]
    [InlineData(false, 403)]
    [InlineData(false, 500)]
    [InlineData(true, 401)]
    [InlineData(true, 403)]
    [InlineData(true, 500)]
    public void Failed_assignment_load_shows_safe_feedback_and_withholds_mutation(bool create, int status)
    {
        using var context = Context(out var api);
        api.Status = (HttpStatusCode)status;
        IRenderedFragment page = create
            ? context.RenderComponent<EquipmentHourMeterCreate>(p => p.Add(x => x.SiteId, SiteId))
            : context.RenderComponent<EquipmentHourMeter>(p => p.Add(x => x.SiteId, SiteId));
        var expected = status == 401 ? "Your session is missing or has expired. Please sign in again."
            : status == 403 ? (create ? "You do not have permission to create hour meter entries." : "You do not have permission to view hour meter records.")
            : (create ? "Unable to load hour meter entry form. Please try again." : "Unable to load hour meter records. Please try again.");
        page.WaitForAssertion(() => Assert.Equal(expected, page.Find("[role=alert]").TextContent.Trim()));
        var suffix = create ? "-create" : "";
        Assert.Equal(status == 500, page.FindAll("#retry-hour-meter" + suffix).Count == 1);
        Assert.Equal(status == 401, page.FindAll("#hour-meter" + suffix + "-sign-in").Count == 1);
        Assert.DoesNotContain("private diagnostic", page.Markup);
        Assert.DoesNotContain("New Hour Entry", page.Markup);
        Assert.DoesNotContain("No hour meter records", page.Markup);
        Assert.Empty(page.FindAll("form, input, select, textarea, .spinner-border"));
        Assert.Contains("Back", page.Markup);
        if (status == 401)
        {
            var route = create ? $"/equipment/hourmeter/create/{SiteId}" : $"/equipment/hourmeter/site/{SiteId}";
            Assert.Equal("/login?returnUrl=" + Uri.EscapeDataString(route),
                page.Find("#hour-meter" + suffix + "-sign-in").GetAttribute("href"));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Retry_restores_active_site_records_or_options(bool create)
    {
        using var context = Context(out var api);
        api.Status = HttpStatusCode.ServiceUnavailable;
        IRenderedFragment page = create
            ? context.RenderComponent<EquipmentHourMeterCreate>(p => p.Add(x => x.SiteId, SiteId))
            : context.RenderComponent<EquipmentHourMeter>(p => p.Add(x => x.SiteId, SiteId));
        page.WaitForAssertion(() => Assert.Single(page.FindAll("[role=alert]")));
        api.Status = null;
        page.Find(create ? "#retry-hour-meter-create" : "#retry-hour-meter").Click();
        page.WaitForAssertion(() => Assert.Contains("Excavator", page.Markup));
        Assert.DoesNotContain("Other site equipment", page.Markup);
        Assert.DoesNotContain("Released equipment", page.Markup);
        Assert.Empty(page.FindAll("[role=alert]"));
        Assert.Contains(create ? "Save Hour Entry" : "New Hour Entry", page.Markup);
    }

    private static TestContext Context(out Api api)
    {
        var context = new TestContext();
        WorkspaceTestAccess.Add(context);
        var service = DispatchProxy.Create<IEquipmentService, Api>();
        api = (Api)(object)service;
        context.Services.AddSingleton(service);
        context.Services.AddSingleton(DispatchProxy.Create<IProjectService, Sites>());
        return context;
    }

    public class Api : DispatchProxy
    {
        public HttpStatusCode? Status { get; set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch {
            nameof(IEquipmentService.GetAssignmentsAsync) => Status.HasValue
                ? Task.FromException<List<EquipmentAssignmentResponse>>(new HttpRequestException("private diagnostic", null, Status))
                : Task.FromResult(new List<EquipmentAssignmentResponse> {
                    new() { EquipmentId = Active, ConstructionSiteId = SiteId },
                    new() { EquipmentId = Other, ConstructionSiteId = Guid.NewGuid() },
                    new() { EquipmentId = Released, ConstructionSiteId = SiteId, ReleasedAtUtc = DateTime.UtcNow }
                }),
            nameof(IEquipmentService.GetHourLogsAsync) => Task.FromResult(new List<EquipmentHourLogResponse> {
                new() { EquipmentId = Active, EquipmentName = "Excavator" },
                new() { EquipmentId = Other, EquipmentName = "Other site equipment" },
                new() { EquipmentId = Released, EquipmentName = "Released equipment" }
            }),
            nameof(IEquipmentService.GetEquipmentAsync) => Task.FromResult(new List<EquipmentResponse> {
                new() { Id = Active, Name = "Excavator" }, new() { Id = Other, Name = "Other site equipment" },
                new() { Id = Released, Name = "Released equipment" }
            }),
            _ => throw new InvalidOperationException("Unexpected equipment mutation.")
        };
    }
    public class Sites : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            Task.FromResult(new List<SiteResponse> { new() { Id = SiteId, Name = "Hamilton Test Site" } });
    }
    public class Auth : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) => Task.CompletedTask;
    }
    private sealed class Factory(HttpClient http) : IHttpClientFactory { public HttpClient CreateClient(string name) => http; }
    private sealed class Response(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent("private diagnostic") });
    }
}
