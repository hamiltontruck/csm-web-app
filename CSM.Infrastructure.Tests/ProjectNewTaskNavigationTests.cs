using System.Reflection;
using Bunit;
using CSM.Web.Components.Pages;
using CSM.Web.Models.Projects;
using CSM.Web.Services;
using CSM.Web.Services.Access;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class ProjectNewTaskNavigationTests
{
    [Theory]
    [InlineData("Supervisor", "Projects_View", false)]
    [InlineData("Supervisor", "Projects_View,Projects_Manage", false)]
    [InlineData("CompanyAdmin", "Projects_View", false)]
    [InlineData("CompanyAdmin", "Projects_View,Projects_Manage", true)]
    [InlineData("SiteManager", "Projects_View,Projects_Manage", true)]
    [InlineData("SuperAdmin", "Projects_View,Projects_Manage", true)]
    public void New_task_visibility_and_navigation_follow_create_authority(string role, string permissions, bool allowed)
    {
        using var context = new TestContext(); var service = Service(context);
        var access = new WorkspaceAccess([role], permissions.Split(','));
        var page = context.RenderComponent<CascadingValue<WorkspaceAccess>>(p => p.Add(x => x.Value, access)
            .Add(x => x.ChildContent, builder => {
                builder.OpenComponent<ProjectDetails>(0); builder.AddAttribute(1, nameof(ProjectDetails.Id), service.ProjectId); builder.CloseComponent();
            }));
        page.WaitForAssertion(() => Assert.Contains("Project Workspace", page.Markup));
        var buttons = page.FindAll("button").Where(b => b.TextContent.Trim() == "New Task").ToArray();
        Assert.Equal(allowed ? 1 : 0, buttons.Length);
        if (allowed)
        {
            buttons.Single().Click();
            Assert.Equal($"http://localhost/projects/{service.ProjectId}/tasks/create", context.Services.GetRequiredService<NavigationManager>().Uri);
        }
    }

    [Fact]
    public void Project_task_creation_route_exists()
        => Assert.Contains(typeof(CreateWorkTask).GetCustomAttributes<RouteAttribute>(),
            route => route.Template == "/projects/{ProjectId:guid}/tasks/create");

    [Fact]
    public void Project_creation_selects_only_project_phases_and_submits_the_chosen_parent()
    {
        using var context = new TestContext(); var service = Service(context);
        var page = context.RenderComponent<CreateWorkTask>(p => p.Add(x => x.ProjectId, service.ProjectId));
        page.WaitForAssertion(() => Assert.Single(page.FindAll("#task-phase")));
        Assert.Contains("Foundation", page.Markup);
        Assert.DoesNotContain("Other project phase", page.Markup);
        page.Find("#task-phase").Change(service.PhaseId.ToString());
        page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.NotNull(service.Submitted));
        Assert.Equal(service.ProjectId, service.Submitted!.ProjectId);
        Assert.Equal(service.PhaseId, service.Submitted.ProjectPhaseId);
    }

    [Fact]
    public void Missing_phase_does_not_send_a_create_request()
    {
        using var context = new TestContext(); var service = Service(context);
        var page = context.RenderComponent<CreateWorkTask>(p => p.Add(x => x.ProjectId, service.ProjectId));
        page.WaitForAssertion(() => Assert.Single(page.FindAll("#task-phase")));
        page.Find("form").Submit();
        Assert.Null(service.Submitted);
        Assert.Contains("Select a project phase", page.Markup);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Successful_creation_returns_to_selected_phase_from_either_entry(bool phaseEntry)
    {
        using var context = new TestContext(); var service = Service(context); service.CreateSuccess = true;
        var page = context.RenderComponent<CreateWorkTask>(p => p.Add(x => x.ProjectId, service.ProjectId)
            .Add(x => x.PhaseId, phaseEntry ? service.PhaseId : Guid.Empty));
        page.WaitForAssertion(() => Assert.Single(page.FindAll("#task-phase")));
        if (phaseEntry) Assert.True(page.Find("#task-phase").HasAttribute("disabled"));
        else page.Find("#task-phase").Change(service.PhaseId.ToString());
        page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.Equal($"http://localhost/projects/{service.ProjectId}/phases/{service.PhaseId}",
            context.Services.GetRequiredService<NavigationManager>().Uri));
        Assert.Equal(service.PhaseId, service.Submitted!.ProjectPhaseId);
    }

    [Fact]
    public void Forbidden_phase_lookup_does_not_render_a_creation_form()
    {
        using var context = new TestContext(); var service = Service(context); service.DenyPhases = true;
        var page = context.RenderComponent<CreateWorkTask>(p => p.Add(x => x.ProjectId, service.ProjectId));
        page.WaitForAssertion(() => Assert.Contains("You do not have permission", page.Markup));
        Assert.Empty(page.FindAll("form")); Assert.Null(service.Submitted);
    }

    [Fact]
    public void Project_without_phases_offers_phase_setup_instead_of_an_invalid_submit()
    {
        using var context = new TestContext(); var service = Service(context); service.NoPhases = true;
        var page = context.RenderComponent<CreateWorkTask>(p => p.Add(x => x.ProjectId, service.ProjectId));
        page.WaitForAssertion(() => Assert.Contains("Create a project phase", page.Markup));
        Assert.Empty(page.FindAll("form"));
        Assert.Equal($"/projects/{service.ProjectId}/phases", page.Find("a").GetAttribute("href"));
    }

    private static ProjectServiceProbe Service(TestContext context)
    {
        var proxy = DispatchProxy.Create<IProjectService, ProjectServiceProbe>();
        context.Services.AddSingleton(proxy); return (ProjectServiceProbe)(object)proxy;
    }
    public class ProjectServiceProbe : DispatchProxy
    {
        public Guid ProjectId { get; } = Guid.NewGuid();
        public Guid PhaseId { get; } = Guid.NewGuid();
        public CreateWorkTaskRequest? Submitted;
        public bool CreateSuccess, DenyPhases, NoPhases;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == nameof(IProjectService.GetProjectAsync)) return Task.FromResult<ProjectDetail?>(new() { Id = ProjectId, Name = "Test project", Status = "Active" });
            if (method.Name == nameof(IProjectService.GetProjectPhasesAsync))
            {
                if (DenyPhases) throw new HttpRequestException("Synthetic forbidden", null, System.Net.HttpStatusCode.Forbidden);
                return Task.FromResult(NoPhases ? new List<ProjectPhase>() : new List<ProjectPhase> {
                    new() { Id = PhaseId, ProjectId = ProjectId, Name = "Foundation" },
                    new() { Id = Guid.NewGuid(), ProjectId = Guid.NewGuid(), Name = "Other project phase" } });
            }
            if (method.Name == nameof(IProjectService.CreateTaskAsync)) { Submitted = (CreateWorkTaskRequest)args![0]!; return Task.FromResult((CreateSuccess, "Synthetic validation response")); }
            throw new NotSupportedException(method.Name);
        }
    }
}
