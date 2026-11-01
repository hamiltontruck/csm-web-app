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
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == nameof(IProjectService.GetProjectAsync)) return Task.FromResult<ProjectDetail?>(new() { Id = ProjectId, Name = "Test project", Status = "Active" });
            if (method.Name == nameof(IProjectService.GetProjectPhasesAsync)) return Task.FromResult(new List<ProjectPhase> { new() { Id = PhaseId, ProjectId = ProjectId, Name = "Foundation" } });
            if (method.Name == nameof(IProjectService.CreateTaskAsync)) { Submitted = (CreateWorkTaskRequest)args![0]!; return Task.FromResult((false, "Synthetic validation response")); }
            throw new NotSupportedException(method.Name);
        }
    }
}
