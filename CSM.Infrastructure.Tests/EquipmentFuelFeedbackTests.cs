using System.Net;
using System.Reflection;
using System.Security.Claims;
using Bunit;
using CSM.Api.Controllers;
using CSM.Application.Common.Security;
using CSM.Domain.Constants;
using CSM.Web.Components.Pages;
using CSM.Web.Models.Equipment;
using CSM.Web.Models.Sites;
using CSM.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class EquipmentFuelFeedbackTests
{
    private static readonly Guid SiteId = Guid.Parse("ae408187-2e4a-41b2-85d9-c4ae6f9e4f66");
    private static readonly Guid LogId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ActiveId = Guid.NewGuid();
    private static readonly Guid OtherId = Guid.NewGuid();
    private static readonly Guid ReleasedId = Guid.NewGuid();

    [Fact]
    public async Task Existing_fuel_controller_policy_denies_supervisor()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddAuthorization(options => options.AddPolicy(AppPolicies.ProcurementManagement,
            policy => policy.RequireRole(AppPolicies.ProcurementManagementRoles)));
        using var provider = services.BuildServiceProvider();
        var policy = await AuthorizationPolicy.CombineAsync(
            provider.GetRequiredService<IAuthorizationPolicyProvider>(),
            typeof(EquipmentFuelLogsController).GetCustomAttributes(true).OfType<IAuthorizeData>());
        var actor = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, AppRoles.Supervisor)], "test"));
        Assert.False((await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(actor, null, policy!)).Succeeded);
    }

    [Theory]
    [InlineData(false, 401)]
    [InlineData(false, 403)]
    [InlineData(false, 500)]
    [InlineData(true, 401)]
    [InlineData(true, 403)]
    [InlineData(true, 500)]
    public async Task Fuel_client_preserves_HTTP_status_without_exposing_response_body(bool detail, int status)
    {
        using var http = new HttpClient(new Response((HttpStatusCode)status)) { BaseAddress = new Uri("https://api.test/api/") };
        var service = new EquipmentService(new Factory(http), DispatchProxy.Create<IAuthService, Auth>());
        var error = await Assert.ThrowsAsync<HttpRequestException>(async () => {
            if (detail) await service.GetFuelLogByIdAsync(LogId);
            else await service.GetFuelLogsAsync();
        });
        Assert.Equal((HttpStatusCode)status, error.StatusCode);
        Assert.DoesNotContain("private API diagnostic", error.Message);
    }

    [Theory]
    [InlineData(401, "Your session is missing or has expired. Please sign in again.", false)]
    [InlineData(403, "You do not have permission to view fuel consumption.", false)]
    [InlineData(500, "Unable to load fuel consumption. Please try again.", true)]
    public void Fuel_list_shows_safe_feedback_without_mutation_controls(int status, string expected, bool retry)
    {
        using var context = Context(out var api);
        api.Status = (HttpStatusCode)status;
        var page = context.RenderComponent<EquipmentFuelLogs>(p => p.Add(x => x.SiteId, SiteId));
        page.WaitForAssertion(() => Assert.Equal(expected, page.Find("[role=alert]").TextContent.Trim()));
        Assert.Equal(retry, page.FindAll("#retry-equipment-fuel").Count == 1);
        Assert.Equal(status == 401, page.FindAll("#equipment-fuel-sign-in").Count == 1);
        Assert.DoesNotContain("New Fuel Entry", page.Markup);
        Assert.DoesNotContain("No fuel consumption records found", page.Markup);
        Assert.DoesNotContain("private API diagnostic", page.Markup);
        Assert.Empty(page.FindAll(".spinner-border"));
        Assert.Contains("Back", page.Markup);
    }

    [Fact]
    public void Fuel_retry_preserves_active_assignment_site_filter()
    {
        using var context = Context(out var api);
        api.Status = HttpStatusCode.ServiceUnavailable;
        var page = context.RenderComponent<EquipmentFuelLogs>(p => p.Add(x => x.SiteId, SiteId));
        page.WaitForAssertion(() => Assert.Single(page.FindAll("[role=alert]")));
        api.Status = null;
        page.Find("#retry-equipment-fuel").Click();
        page.WaitForAssertion(() => Assert.Contains("Excavator", page.Markup));
        Assert.DoesNotContain("Other site equipment", page.Markup);
        Assert.DoesNotContain("Released equipment", page.Markup);
        Assert.Contains("New Fuel Entry", page.Markup);
        Assert.Contains("Edit", page.Markup);
        Assert.Empty(page.FindAll("[role=alert]"));
    }

    [Theory]
    [InlineData(false, 401, "Your session is missing or has expired. Please sign in again.", false)]
    [InlineData(false, 403, "You do not have permission to create fuel entries.", false)]
    [InlineData(false, 500, "Unable to load fuel entry form. Please try again.", true)]
    [InlineData(true, 401, "Your session is missing or has expired. Please sign in again.", false)]
    [InlineData(true, 403, "You do not have permission to edit fuel entries.", false)]
    [InlineData(true, 500, "Unable to load fuel entry. Please try again.", true)]
    public void Direct_fuel_forms_handle_errors_without_rendering_fields(bool edit, int status, string expected, bool retry)
    {
        using var context = Context(out var api);
        api.Status = (HttpStatusCode)status;
        IRenderedFragment page = edit
            ? context.RenderComponent<EquipmentFuelLogEdit>(p => p.Add(x => x.SiteId, SiteId).Add(x => x.FuelLogId, LogId))
            : context.RenderComponent<EquipmentFuelLogCreate>(p => p.Add(x => x.SiteId, SiteId));
        page.WaitForAssertion(() => Assert.Equal(expected, page.Find("[role=alert]").TextContent.Trim()));
        Assert.Empty(page.FindAll("form, input, select, textarea"));
        var suffix = edit ? "edit" : "create";
        Assert.Equal(retry, page.FindAll("#retry-equipment-fuel-" + suffix).Count == 1);
        Assert.Contains("Back", page.Markup);
        if (status == 401)
        {
            var route = edit ? $"/equipment/fuel/edit/{SiteId}/{LogId}" : $"/equipment/fuel/create/{SiteId}";
            Assert.Equal("/login?returnUrl=" + Uri.EscapeDataString(route),
                page.Find("#equipment-fuel-" + suffix + "-sign-in").GetAttribute("href"));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Allowed_user_can_open_fuel_form_with_active_site_equipment(bool edit)
    {
        using var context = Context(out _);
        IRenderedFragment page = edit
            ? context.RenderComponent<EquipmentFuelLogEdit>(p => p.Add(x => x.SiteId, SiteId).Add(x => x.FuelLogId, LogId))
            : context.RenderComponent<EquipmentFuelLogCreate>(p => p.Add(x => x.SiteId, SiteId));
        page.WaitForAssertion(() => Assert.Single(page.FindAll("form")));
        Assert.Contains(edit ? "Update Fuel Entry" : "Save Fuel Entry", page.Markup);
        Assert.Contains("Excavator", page.Markup);
        Assert.DoesNotContain("Other site equipment", page.Markup);
        Assert.DoesNotContain("Released equipment", page.Markup);
        Assert.Empty(page.FindAll("[role=alert]"));
    }

    private static TestContext Context(out Api api)
    {
        var context = new TestContext();
        var service = DispatchProxy.Create<IEquipmentService, Api>();
        api = (Api)(object)service;
        context.Services.AddSingleton(service);
        context.Services.AddSingleton(DispatchProxy.Create<IProjectService, Sites>());
        return context;
    }

    private static EquipmentFuelLogResponse Log() => new() {
        Id = LogId, EquipmentId = ActiveId, SiteId = SiteId, EquipmentName = "Excavator",
        RefueledAtUtc = DateTime.UtcNow, Liters = 10m, UnitPrice = 2m, TotalCost = 20m, MeterReading = 100m
    };

    public class Api : DispatchProxy
    {
        public HttpStatusCode? Status { get; set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch {
            nameof(IEquipmentService.GetFuelLogsAsync) => Status.HasValue
                ? Task.FromException<List<EquipmentFuelLogResponse>>(new HttpRequestException("private API diagnostic", null, Status))
                : Task.FromResult(new List<EquipmentFuelLogResponse> {
                    Log(), new() { EquipmentId = OtherId, EquipmentName = "Other site equipment" },
                    new() { EquipmentId = ReleasedId, EquipmentName = "Released equipment" }
                }),
            nameof(IEquipmentService.GetFuelLogByIdAsync) => Status.HasValue
                ? Task.FromException<EquipmentFuelLogResponse?>(new HttpRequestException("private API diagnostic", null, Status))
                : Task.FromResult<EquipmentFuelLogResponse?>((Guid)args![0]! == LogId ? Log() : null),
            nameof(IEquipmentService.GetAssignmentsAsync) => Task.FromResult(new List<EquipmentAssignmentResponse> {
                new() { EquipmentId = ActiveId, ConstructionSiteId = SiteId },
                new() { EquipmentId = OtherId, ConstructionSiteId = Guid.NewGuid() },
                new() { EquipmentId = ReleasedId, ConstructionSiteId = SiteId, ReleasedAtUtc = DateTime.UtcNow }
            }),
            nameof(IEquipmentService.GetEquipmentAsync) => Task.FromResult(new List<EquipmentResponse> {
                new() { Id = ActiveId, Name = "Excavator" }, new() { Id = OtherId, Name = "Other site equipment" },
                new() { Id = ReleasedId, Name = "Released equipment" }
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
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent("private API diagnostic") });
    }
}
