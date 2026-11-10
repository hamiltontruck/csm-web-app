using System.Net;
using System.Reflection;
using Bunit;
using CSM.Web.Components.Pages;
using CSM.Web.Models.Projects;
using CSM.Web.Models.Sites;
using CSM.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class SiteDetailsAccessTests
{
    private static readonly Guid SiteId = Guid.NewGuid();

    [Fact]
    public void Site_reader_without_projects_permission_sees_site_without_forbidden_lookup_or_controls()
    {
        using var context = Context(false, out var api);
        var page = context.RenderComponent<SiteDetails>(p => p.Add(x => x.SiteId, SiteId));
        page.WaitForAssertion(() => Assert.Contains("Hamilton Test Site", page.Markup));
        Assert.Equal(0, api.ProjectCalls);
        Assert.Empty(page.FindAll("select"));
        Assert.DoesNotContain("Update Status", page.Markup);
        Assert.DoesNotContain("Edit Site", page.Markup);
        Assert.DoesNotContain("Contract Value", page.Markup);
        Assert.DoesNotContain("Site Projects", page.Markup);
        Assert.DoesNotContain("Workforce", page.Markup);
        Assert.DoesNotContain("Compliance", page.Markup);
        Assert.Contains("Inventory", page.Markup);
        Assert.Empty(page.FindAll("[role=alert]"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Project_reader_keeps_site_projects_with_status_controls_only_when_authorized(bool canManageSite)
    {
        using var context = Context(true, out var api, canManageSite);
        var page = context.RenderComponent<SiteDetails>(p => p.Add(x => x.SiteId, SiteId));
        page.WaitForAssertion(() => Assert.Contains("Visible site project", page.Markup));
        Assert.DoesNotContain("Other site project", page.Markup);
        Assert.Equal(1, api.ProjectCalls);
        if (!canManageSite)
        {
            Assert.Empty(page.FindAll("select"));
            Assert.DoesNotContain("Update Status", page.Markup);
            Assert.DoesNotContain("Edit Site", page.Markup);
            Assert.Equal(0, api.StatusWrites);
            return;
        }
        page.Find("select").Change("Active");
        page.FindAll("button").Single(b => b.TextContent.Trim() == "Update Status").Click();
        page.WaitForAssertion(() => Assert.Equal(1, api.StatusWrites));
        Assert.Equal(SiteStatus.Active, api.LastStatus);
        Assert.Empty(page.FindAll("[role=alert]"));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    public void Site_lookup_failure_renders_feedback_instead_of_breaking_the_circuit(HttpStatusCode status)
    {
        using var context = Context(false, out var api);
        api.Failure = status;
        var page = context.RenderComponent<SiteDetails>(p => p.Add(x => x.SiteId, SiteId));
        page.WaitForAssertion(() => Assert.Single(page.FindAll("[role=alert]")));
        Assert.DoesNotContain("Loading site", page.Markup);
        Assert.DoesNotContain("private diagnostic", page.Markup);
        Assert.Equal(0, api.ProjectCalls);
        if (status == HttpStatusCode.Unauthorized)
            Assert.Equal("/login", page.Find("#site-details-sign-in").GetAttribute("href"));
        Assert.Empty(page.FindAll("#retry-site-details"));
    }

    [Fact]
    public void A_transient_site_failure_can_be_retried_without_reloading_the_application()
    {
        using var context = Context(false, out var api);
        api.Failure = HttpStatusCode.ServiceUnavailable;
        var page = context.RenderComponent<SiteDetails>(p => p.Add(x => x.SiteId, SiteId));
        page.WaitForAssertion(() => Assert.Single(page.FindAll("[role=alert]")));
        api.Failure = null;
        page.Find("#retry-site-details").Click();
        page.WaitForAssertion(() => Assert.Contains("Hamilton Test Site", page.Markup));
        Assert.Empty(page.FindAll("[role=alert]"));
        Assert.Equal(2, api.SiteCalls);
        Assert.Equal(0, api.ProjectCalls);
    }

    private static TestContext Context(bool manage, out Api api, bool allowSiteWrites = true)
    {
        var context = new TestContext();
        WorkspaceTestAccess.Add(context, manage ? ["SiteManager"] : ["ProcurementOfficer"],
            manage ? (allowSiteWrites ? ["Sites_View", "Sites_Manage", "Projects_View"] : ["Sites_View", "Projects_View"])
                : ["Sites_View", "Procurement_Inventory_View"]);
        var service = DispatchProxy.Create<IProjectService, Api>();
        api = (Api)(object)service;
        api.AllowProjects = manage;
        context.Services.AddSingleton(service);
        return context;
    }

    public class Api : DispatchProxy
    {
        public bool AllowProjects;
        public HttpStatusCode? Failure;
        public int SiteCalls;
        public int ProjectCalls;
        public int StatusWrites;
        public SiteStatus LastStatus;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            switch (method!.Name)
            {
                case nameof(IProjectService.GetSiteAsync):
                    SiteCalls++;
                    Assert.Equal(SiteId, (Guid)args![0]!);
                    return Failure.HasValue ? Task.FromException<SiteResponse>(new HttpRequestException("private diagnostic", null, Failure)) :
                        Task.FromResult(new SiteResponse { Id = SiteId, Name = "Hamilton Test Site", SiteCode = "HBG-TEST-001", Status = "Planning", Country = "Ethiopia" });
                case nameof(IProjectService.GetProjectsAsync):
                    ProjectCalls++;
                    return AllowProjects ? Task.FromResult(new List<ProjectSummary> {
                        new() { Id = Guid.NewGuid(), ConstructionSiteId = SiteId, Name = "Visible site project", Status = "Active", ContractValue = 1000m },
                        new() { Id = Guid.NewGuid(), ConstructionSiteId = Guid.NewGuid(), Name = "Other site project", Status = "Active", ContractValue = 9000m }
                    }) : Task.FromException<List<ProjectSummary>>(new HttpRequestException("Forbidden projects", null, HttpStatusCode.Forbidden));
                case nameof(IProjectService.ChangeSiteStatusAsync):
                    Assert.Equal(SiteId, (Guid)args![0]!);
                    LastStatus = (SiteStatus)args[1]!; StatusWrites++;
                    return Task.FromResult(new SiteResponse { Id = SiteId, Name = "Hamilton Test Site", Status = LastStatus.ToString() });
                default: throw new InvalidOperationException(method.Name);
            }
        }
    }
}
