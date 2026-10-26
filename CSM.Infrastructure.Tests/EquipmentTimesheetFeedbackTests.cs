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

public sealed class EquipmentTimesheetFeedbackTests
{
    private static readonly Guid SiteId = Guid.Parse("ae408187-2e4a-41b2-85d9-c4ae6f9e4f66");
    private static readonly Guid Active = Guid.NewGuid();
    private static readonly Guid Other = Guid.NewGuid();
    private static readonly Guid Released = Guid.NewGuid();

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(500)]
    public async Task Timesheet_client_preserves_status_without_API_body(int status)
    {
        using var http = new HttpClient(new Response((HttpStatusCode)status)) { BaseAddress = new Uri("https://api.test/api/") };
        var service = new EquipmentService(new Factory(http), DispatchProxy.Create<IAuthService, Auth>());
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => service.GetTimesheetsAsync());
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
            ? context.RenderComponent<EquipmentTimesheetCreate>(p => p.Add(x => x.SiteId, SiteId))
            : context.RenderComponent<EquipmentTimesheets>(p => p.Add(x => x.SiteId, SiteId));
        var expected = status == 401 ? "Your session is missing or has expired. Please sign in again."
            : status == 403 ? (create ? "You do not have permission to create equipment timesheets." : "You do not have permission to view equipment timesheets.")
            : (create ? "Unable to load equipment timesheet form. Please try again." : "Unable to load equipment timesheets. Please try again.");
        page.WaitForAssertion(() => Assert.Equal(expected, page.Find("[role=alert]").TextContent.Trim()));
        var suffix = create ? "-create" : "";
        Assert.Equal(status == 500, page.FindAll("#retry-equipment-timesheet" + suffix).Count == 1);
        Assert.Equal(status == 401, page.FindAll("#equipment-timesheet" + suffix + "-sign-in").Count == 1);
        Assert.DoesNotContain("private diagnostic", page.Markup);
        Assert.DoesNotContain("New Timesheet", page.Markup);
        Assert.DoesNotContain("Daily Report", page.Markup);
        Assert.DoesNotContain("No daily timesheets", page.Markup);
        Assert.Empty(page.FindAll("form, input, select, textarea, .spinner-border"));
        Assert.Contains("Back", page.Markup);
        if (status == 401)
        {
            var route = create ? $"/equipment/timesheet/create/{SiteId}" : $"/equipment/timesheet/site/{SiteId}";
            Assert.Equal("/login?returnUrl=" + Uri.EscapeDataString(route),
                page.Find("#equipment-timesheet" + suffix + "-sign-in").GetAttribute("href"));
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
            ? context.RenderComponent<EquipmentTimesheetCreate>(p => p.Add(x => x.SiteId, SiteId))
            : context.RenderComponent<EquipmentTimesheets>(p => p.Add(x => x.SiteId, SiteId));
        page.WaitForAssertion(() => Assert.Single(page.FindAll("[role=alert]")));
        api.Status = null;
        page.Find(create ? "#retry-equipment-timesheet-create" : "#retry-equipment-timesheet").Click();
        page.WaitForAssertion(() => Assert.Contains("Excavator", page.Markup));
        Assert.DoesNotContain("Other site equipment", page.Markup);
        Assert.DoesNotContain("Released equipment", page.Markup);
        Assert.Empty(page.FindAll("[role=alert]"));
        Assert.Contains(create ? "Save Timesheet" : "New Timesheet", page.Markup);
    }


    [Theory]
    [InlineData(false, 401)]
    [InlineData(false, 403)]
    [InlineData(false, 500)]
    [InlineData(true, 401)]
    [InlineData(true, 403)]
    [InlineData(true, 500)]
    public void Direct_edit_and_report_failures_hide_fields_and_totals(bool report, int status)
    {
        using var context = Context(out var api);
        api.Status = (HttpStatusCode)status;
        IRenderedFragment page = report
            ? context.RenderComponent<EquipmentTimesheetReport>(p => p.Add(x => x.SiteId, SiteId))
            : context.RenderComponent<EquipmentTimesheetEdit>(p => p.Add(x => x.Id, Active));
        page.WaitForAssertion(() => Assert.Single(page.FindAll("[role=alert]")));
        Assert.DoesNotContain("private diagnostic", page.Markup);
        Assert.DoesNotContain("Site Total Hours", page.Markup);
        Assert.Empty(page.FindAll("form, input, select, textarea, .spinner-border"));
        var suffix = report ? "-report" : "-edit";
        Assert.Equal(status == 500, page.FindAll("#retry-equipment-timesheet" + suffix).Count == 1);
        Assert.Equal(status == 401, page.FindAll("#equipment-timesheet" + suffix + "-sign-in").Count == 1);
        Assert.Contains("Back", page.Markup);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Edit_and_report_retry_recover_after_transient_failure(bool report)
    {
        using var context = Context(out var api);
        api.Status = HttpStatusCode.ServiceUnavailable;
        IRenderedFragment page = report
            ? context.RenderComponent<EquipmentTimesheetReport>(p => p.Add(x => x.SiteId, SiteId))
            : context.RenderComponent<EquipmentTimesheetEdit>(p => p.Add(x => x.Id, Active));
        page.WaitForAssertion(() => Assert.Single(page.FindAll("[role=alert]")));
        api.Status = null;
        page.Find(report ? "#retry-equipment-timesheet-report" : "#retry-equipment-timesheet-edit").Click();
        page.WaitForAssertion(() => Assert.Contains(report ? "Site Total Hours" : "Update Timesheet", page.Markup));
        Assert.Empty(page.FindAll("[role=alert]"));
        if (report)
        {
            Assert.Contains("Excavator", page.Markup);
            Assert.DoesNotContain("Other site equipment", page.Markup);
        }
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(500)]
    public async Task Detail_client_preserves_status_without_API_body(int status)
    {
        using var http = new HttpClient(new Response((HttpStatusCode)status)) { BaseAddress = new Uri("https://api.test/api/") };
        var service = new EquipmentService(new Factory(http), DispatchProxy.Create<IAuthService, Auth>());
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => service.GetTimesheetByIdAsync(Active));
        Assert.Equal((HttpStatusCode)status, error.StatusCode);
        Assert.DoesNotContain("private diagnostic", error.Message);
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
            nameof(IEquipmentService.GetTimesheetByIdAsync) => Status.HasValue
                ? Task.FromException<EquipmentTimesheetResponse>(new HttpRequestException("private diagnostic", null, Status))
                : Task.FromResult(new EquipmentTimesheetResponse { Id = Active, EquipmentId = Active, SiteId = SiteId, WorkDate = DateTime.Today }),
            nameof(IEquipmentService.GetTimesheetsAsync) => Status.HasValue
                ? Task.FromException<List<EquipmentTimesheetResponse>>(new HttpRequestException("private diagnostic", null, Status))
                : Task.FromResult(new List<EquipmentTimesheetResponse> {
                new() { Id = Active, EquipmentId = Active, SiteId = SiteId, WorkDate = DateTime.Today, EquipmentName = "Excavator", WorkedHours = 2m },
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
