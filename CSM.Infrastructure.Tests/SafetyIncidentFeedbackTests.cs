using System.Net;
using System.Reflection;
using System.Security.Claims;
using Bunit;
using CSM.Api.Controllers;
using CSM.Application.Common.Exceptions;
using CSM.Application.Common.Security;
using CSM.Web.Components.Pages;
using CSM.Web.Models.Compliance;
using CSM.Web.Models.HRM;
using CSM.Web.Models.Sites;
using CSM.Web.Models.Projects;
using CSM.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;
public sealed class SafetyIncidentFeedbackTests
{
    [Theory]
    [InlineData("SuperAdmin", true)] [InlineData("CompanyAdmin", true)] [InlineData("SiteManager", true)]
    [InlineData("Supervisor", false)] [InlineData("Administrator", false)] [InlineData("Storekeeper", false)]
    [InlineData("ProcurementOfficer", false)] [InlineData("SafetyOfficer", false)]
    public async Task Controller_roles_match_existing_service_contract(string role, bool allowed)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddAuthorization(o => o.AddPolicy(AppPolicies.ProcurementManagement, p => p.RequireRole(AppPolicies.ProcurementManagementRoles)));
        using var provider = services.AddGrantedBackendPermissions(role).BuildServiceProvider();
        var actor = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "test"));
        foreach (var method in new[] { "GetAll", "GetById", "Create", "Update" })
        {
            var metadata = typeof(SafetyIncidentsController).GetCustomAttributes(true).OfType<IAuthorizeData>()
                .Concat(typeof(SafetyIncidentsController).GetMethod(method)!.GetCustomAttributes(true).OfType<IAuthorizeData>());
            var policy = await AuthorizationPolicy.CombineAsync(provider.GetRequiredService<IAuthorizationPolicyProvider>(), metadata);
            Assert.Equal(allowed, (await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(BackendAuthorizationFixture.WithGrantedIdentity(actor), null, policy!)).Succeeded);
        }
    }

    [Theory]
    [InlineData("You are not authorized to access this safety incident.", 403)]
    [InlineData("Safety incident was not found.", 404)]
    [InlineData("Incident number is required.", 400)]
    public async Task Controller_preserves_access_missing_and_validation_semantics(string message, int expected)
    {
        var api = DispatchProxy.Create<CSM.Application.Compliance.SafetyIncidents.ISafetyIncidentService, DomainApi>();
        ((DomainApi)(object)api).Message = message;
        var controller = new SafetyIncidentsController(api) { ControllerContext = new ControllerContext {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())], "test")) } } };
        var result = await controller.GetById(Guid.NewGuid(), default);
        Assert.Equal(expected, Assert.IsAssignableFrom<ObjectResult>(result.Result).StatusCode);
    }

    [Theory]
    [InlineData("list", 401)] [InlineData("list", 403)] [InlineData("list", 500)]
    [InlineData("create", 401)] [InlineData("create", 403)] [InlineData("create", 500)]
    [InlineData("edit", 401)] [InlineData("edit", 403)] [InlineData("edit", 500)]
    [InlineData("details", 401)] [InlineData("details", 403)] [InlineData("details", 500)]
    public void Protected_load_failure_has_no_partial_form_data_or_mutation_button(string route, int status)
    {
        using var context = Context(out var state);
        state.Status = (HttpStatusCode)status;
        var page = Render(context, route);
        page.WaitForAssertion(() => Assert.Single(page.FindAll("[role=alert]")));
        Assert.Empty(page.FindAll("table, form, input, select, textarea, dl"));
        Assert.DoesNotContain("No safety incidents found", page.Markup);
        Assert.DoesNotContain("private diagnostic", page.Markup);
        Assert.DoesNotContain(page.FindAll("button"), b => b.TextContent.Trim() is "Add Safety Incident" or "Edit");
        Assert.Equal(status == 401, page.FindAll("#safety-" + route + "-sign-in").Count == 1);
        Assert.Equal(status == 500, page.FindAll("#retry-safety-" + route).Count == 1);
    }

    [Theory]
    [InlineData("list")] [InlineData("create")] [InlineData("edit")] [InlineData("details")]
    public void Retry_recovers_protected_data_without_saving(string route)
    {
        using var context = Context(out var state);
        state.Status = HttpStatusCode.ServiceUnavailable;
        var page = Render(context, route);
        page.WaitForAssertion(() => Assert.Single(page.FindAll("#retry-safety-" + route)));
        state.Status = null;
        page.Find("#retry-safety-" + route).Click();
        page.WaitForAssertion(() => Assert.Contains(route == "create" ? "Test Site" : "INC-TEST-1", page.Markup));
        Assert.Empty(page.FindAll("[role=alert]"));
    }

    [Theory]
    [InlineData("edit")] [InlineData("details")]
    public void Missing_incident_has_no_form_or_retry(string route)
    {
        using var context = Context(out var state);
        state.Missing = true;
        var page = Render(context, route);
        page.WaitForAssertion(() => Assert.Contains("Safety incident was not found.", page.Markup));
        Assert.Empty(page.FindAll("form, input, select, textarea, #retry-safety-" + route));
    }


    [Fact]
    public void Optional_employee_denial_keeps_creation_available_with_disabled_choices()
    {
        using var context = Context(out var state);
        state.OptionsDenied = true;
        var page = context.RenderComponent<SafetyIncidentCreate>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("form")));
        Assert.Contains("Employee choices are unavailable", page.Markup);
        Assert.True(page.Find("#safety-employee").HasAttribute("disabled"));
        Assert.True(page.Find("#safety-investigator").HasAttribute("disabled"));
        Assert.False(page.Find("button[type=submit]").HasAttribute("disabled"));
        Assert.DoesNotContain("private diagnostic", page.Markup);
    }

    [Fact]
    public void Creation_without_optional_employee_choices_submits_the_incident()
    {
        using var context = Context(out var state);
        state.OptionsDenied = true;
        var page = context.RenderComponent<SafetyIncidentCreate>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("form")));
        page.Find("select").Change("22222222-2222-2222-2222-222222222222");
        page.FindAll("input").First(x => x.GetAttribute("type") is null or "text").Change("INC-SM-TEST-001");
        page.FindAll("input").Last(x => x.GetAttribute("type") is null or "text").Change("Test site work area");
        page.FindAll("select").First(x => x.QuerySelector("option[value='6']") is not null).Change("1");
        page.FindAll("textarea").First().Change("SiteManager safety incident test, no actual injury.");
        page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.Equal(1, state.SaveCalls));
        Assert.Equal(Guid.Parse("22222222-2222-2222-2222-222222222222"), state.Saved!.ConstructionSiteId);
        Assert.Equal("INC-SM-TEST-001", state.Saved.IncidentNumber);
        Assert.Null(state.Saved.EmployeeId);
        Assert.Null(state.Saved.InvestigatedBy);
        Assert.EndsWith("/compliance/safety-incidents",
            context.Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>().Uri);
    }

    [Theory]
    [InlineData(401)] [InlineData(500)]
    public void Employee_session_or_service_failure_still_blocks_creation(int status)
    {
        using var context = Context(out var state);
        state.OptionsStatus = (HttpStatusCode)status;
        var page = context.RenderComponent<SafetyIncidentCreate>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("[role=alert]")));
        Assert.Empty(page.FindAll("form"));
        Assert.Equal(status == 401, page.FindAll("#safety-create-sign-in").Count == 1);
        Assert.DoesNotContain("private diagnostic", page.Markup);
        Assert.Equal(0, state.SaveCalls);
    }

    [Fact]
    public void Required_site_denial_still_blocks_creation()
    {
        using var context = Context(out var state);
        state.SitesDenied = true;
        var page = context.RenderComponent<SafetyIncidentCreate>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("[role=alert]")));
        Assert.Empty(page.FindAll("form"));
        Assert.Equal(0, state.SaveCalls);
    }


    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Submit_uses_current_input_text_without_waiting_for_blur(bool allFieldsUseInput)
    {
        using var context = Context(out var state);
        state.OptionsDenied = true;
        var page = context.RenderComponent<SafetyIncidentCreate>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("form")));
        page.Find("select").Change("22222222-2222-2222-2222-222222222222");
        if (allFieldsUseInput)
        {
            page.FindAll("input").First(x => x.GetAttribute("type") is null or "text").Input("INC-SM-TEST-INPUT");
            page.FindAll("input").Last(x => x.GetAttribute("type") is null or "text").Input("Test work area");
        }
        else
        {
            page.FindAll("input").First(x => x.GetAttribute("type") is null or "text").Change("INC-SM-TEST-INPUT");
            page.FindAll("input").Last(x => x.GetAttribute("type") is null or "text").Change("Test work area");
        }
        page.FindAll("select").First(x => x.QuerySelector("option[value='6']") is not null).Change("1");
        page.FindAll("textarea").First().Input("SiteManager safety incident test. No actual incident or injury occurred.");
        page.FindAll("textarea").ElementAt(1).Input("Test immediate action");
        page.FindAll("textarea").ElementAt(2).Input("Test root cause");
        page.FindAll("textarea").ElementAt(3).Input("Test corrective action");
        page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.Equal(1, state.SaveCalls));
        Assert.Equal("INC-SM-TEST-INPUT", state.Saved!.IncidentNumber);
        Assert.Equal("Test work area", state.Saved.LocationDescription);
        Assert.Equal("SiteManager safety incident test. No actual incident or injury occurred.", state.Saved.Description);
        Assert.Equal("Test immediate action", state.Saved.ImmediateActionTaken);
        Assert.Equal("Test root cause", state.Saved.RootCause);
        Assert.Equal("Test corrective action", state.Saved.CorrectiveAction);
        Assert.DoesNotContain("Description is required.", page.Markup);
    }


    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Edit_without_employee_choices_preserves_existing_assignments_on_save(bool assigned)
    {
        using var context = Context(out var state);
        state.OptionsDenied = true;
        state.ExistingPeople = assigned;
        var page = context.RenderComponent<SafetyIncidentEdit>(p => p.Add(x => x.Id, Guid.Parse("11111111-1111-1111-1111-111111111111")));
        page.WaitForAssertion(() => Assert.Single(page.FindAll("form")));
        Assert.True(page.Find("#safety-edit-employee").HasAttribute("disabled"));
        Assert.True(page.Find("#safety-edit-investigator").HasAttribute("disabled"));
        Assert.Contains("Existing employee and investigator selections will be kept", page.Markup);
        if (assigned)
        {
            Assert.NotNull(page.Find("#safety-edit-employee option[value='44444444-4444-4444-4444-444444444444']"));
            Assert.NotNull(page.Find("#safety-edit-investigator option[value='55555555-5555-5555-5555-555555555555']"));
        }
        page.FindAll("textarea").First().Change("Updated test incident description");
        page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.Equal(1, state.UpdateCalls));
        Assert.Equal("Updated test incident description", state.Updated!.Description);
        Assert.Equal(assigned ? Guid.Parse("44444444-4444-4444-4444-444444444444") : (Guid?)null, state.Updated.EmployeeId);
        Assert.Equal(assigned ? Guid.Parse("55555555-5555-5555-5555-555555555555") : (Guid?)null, state.Updated.InvestigatedBy);
        Assert.Equal(new DateTime(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc), state.Updated.ReportedAtUtc);
        Assert.DoesNotContain("private diagnostic", page.Markup);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(500)]
    public void Edit_employee_session_or_service_failure_still_blocks_the_form(int status)
    {
        using var context = Context(out var state);
        state.OptionsStatus = (HttpStatusCode)status;
        var page = Render(context, "edit");
        page.WaitForAssertion(() => Assert.Single(page.FindAll("[role=alert]")));
        Assert.Empty(page.FindAll("form"));
        Assert.Equal(status == 401, page.FindAll("#safety-edit-sign-in").Count == 1);
        Assert.Equal(0, state.UpdateCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Edit_required_site_or_project_denial_still_blocks_the_form(bool project)
    {
        using var context = Context(out var state);
        state.SitesDenied = !project;
        state.ProjectsDenied = project;
        var page = Render(context, "edit");
        page.WaitForAssertion(() => Assert.Single(page.FindAll("[role=alert]")));
        Assert.Empty(page.FindAll("form"));
        Assert.Equal(0, state.UpdateCalls);
    }

    [Fact]
    public void Edit_submits_the_latest_six_text_values_without_blur()
    {
        using var context = Context(out var state);
        var page = context.RenderComponent<SafetyIncidentEdit>(p => p.Add(x => x.Id, Guid.Parse("11111111-1111-1111-1111-111111111111")));
        page.WaitForAssertion(() => Assert.Single(page.FindAll("form")));
        page.FindAll("input").First(x => x.GetAttribute("type") is null or "text").Input("INC-EDIT-INPUT");
        page.FindAll("input").Last(x => x.GetAttribute("type") is null or "text").Input("Updated work area");
        page.FindAll("textarea").First().Input("Updated test description");
        page.FindAll("textarea").ElementAt(1).Input("Updated immediate action");
        page.FindAll("textarea").ElementAt(2).Input("Updated root cause");
        page.FindAll("textarea").ElementAt(3).Input("Updated corrective action");
        page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.Equal(1, state.UpdateCalls));
        Assert.Equal("INC-EDIT-INPUT", state.Updated!.IncidentNumber);
        Assert.Equal("Updated work area", state.Updated.LocationDescription);
        Assert.Equal("Updated test description", state.Updated.Description);
        Assert.Equal("Updated immediate action", state.Updated.ImmediateActionTaken);
        Assert.Equal("Updated root cause", state.Updated.RootCause);
        Assert.Equal("Updated corrective action", state.Updated.CorrectiveAction);
    }

    private static IRenderedFragment Render(TestContext context, string route) => route switch {
        "list" => context.RenderComponent<SafetyIncidents>(),
        "create" => context.RenderComponent<SafetyIncidentCreate>(),
        "edit" => context.RenderComponent<SafetyIncidentEdit>(p => p.Add(x => x.Id, Guid.Parse("11111111-1111-1111-1111-111111111111"))),
        _ => context.RenderComponent<SafetyIncidentDetails>(p => p.Add(x => x.Id, Guid.Parse("11111111-1111-1111-1111-111111111111")))
    };
    private static TestContext Context(out State state)
    {
        var context = new TestContext(); state = new();
        var api = DispatchProxy.Create<IComplianceService, Api>(); ((Api)(object)api).State = state;
        var employee = DispatchProxy.Create<IEmployeeService, EmployeeApi>(); ((EmployeeApi)(object)employee).State = state;
        context.Services.AddSingleton(api); context.Services.AddSingleton(employee);
        var project = DispatchProxy.Create<IProjectService, ProjectApi>(); ((ProjectApi)(object)project).State = state;
        context.Services.AddSingleton(project);
        return context;
    }
    public sealed class State { public HttpStatusCode? Status; public bool OptionsDenied; public bool Missing; public HttpStatusCode? OptionsStatus; public bool SitesDenied; public int SaveCalls; public CreateSafetyIncidentRequest? Saved; public bool ExistingPeople; public bool ProjectsDenied; public int UpdateCalls; public UpdateSafetyIncidentRequest? Updated; }
    public class Api : DispatchProxy
    {
        public State State = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            var row = new SafetyIncidentResponse { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                IncidentNumber = "INC-TEST-1", ConstructionSiteName = "Test Site", Severity = "Minor", Status = "Reported",
                ConstructionSiteId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                OccurredAtUtc = new DateTime(2026, 11, 2, 8, 0, 0, DateTimeKind.Utc),
                ReportedAtUtc = new DateTime(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc),
                Description = "Original test description", LocationDescription = "Original work area",
                EmployeeId = State.ExistingPeople ? Guid.Parse("44444444-4444-4444-4444-444444444444") : null,
                InvestigatedBy = State.ExistingPeople ? Guid.Parse("55555555-5555-5555-5555-555555555555") : null };
            if (method!.Name == nameof(IComplianceService.GetSafetyIncidentsAsync))
                return State.Status.HasValue ? Task.FromException<IReadOnlyList<SafetyIncidentResponse>>(Error(State.Status.Value))
                    : Task.FromResult<IReadOnlyList<SafetyIncidentResponse>>(new[] { row });
            if (method.Name == nameof(IComplianceService.GetSafetyIncidentAsync))
                return State.Status.HasValue ? Task.FromException<SafetyIncidentResponse?>(Error(State.Status.Value))
                    : Task.FromResult<SafetyIncidentResponse?>(State.Missing ? null : row);
            if (method.Name == nameof(IComplianceService.CreateSafetyIncidentAsync))
            {
                State.SaveCalls++;
                State.Saved = (CreateSafetyIncidentRequest)args![0]!;
                return Task.FromResult(row);
            }
            if (method.Name == nameof(IComplianceService.UpdateSafetyIncidentAsync))
            {
                State.UpdateCalls++;
                State.Updated = (UpdateSafetyIncidentRequest)args![1]!;
                return Task.FromResult(row);
            }
            throw new InvalidOperationException("Unexpected incident mutation.");
        }
    }
    public class EmployeeApi : DispatchProxy
    {
        public State State = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => (State.OptionsDenied || State.OptionsStatus.HasValue)
            ? Task.FromException<IReadOnlyList<EmployeeResponse>>(Error(State.OptionsStatus ?? HttpStatusCode.Forbidden))
            : Task.FromResult<IReadOnlyList<EmployeeResponse>>(Array.Empty<EmployeeResponse>());
    }
    public class ProjectApi : DispatchProxy
    {
        public State State = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name == nameof(IProjectService.GetSitesAsync)
            ? (State.SitesDenied ? Task.FromException<List<SiteResponse>>(Error(HttpStatusCode.Forbidden)) : Task.FromResult(new List<SiteResponse> { new SiteResponse { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Name = "Test Site" } }))
            : (State.ProjectsDenied ? Task.FromException<List<ProjectResponse>>(Error(HttpStatusCode.Forbidden)) : Task.FromResult(new List<ProjectResponse>()));
    }
    public class DomainApi : DispatchProxy
    {
        public string Message = "";
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            Task.FromException<CSM.Application.Compliance.SafetyIncidents.Dtos.SafetyIncidentResponse>(new SafetyIncidentManagementException(Message));
    }
    private static HttpRequestException Error(HttpStatusCode status) => new("private diagnostic", null, status);
}
