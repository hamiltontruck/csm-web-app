using System.Net;
using System.Reflection;
using System.Security.Claims;
using Bunit;
using CSM.Api.Controllers;
using CSM.Application.Common.Exceptions;
using CSM.Application.Common.Security;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Projects;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Services;
using CSM.Web.Components.Pages;
using ProjectPhase = CSM.Web.Models.Projects.ProjectPhase;
using CSM.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class SupervisorProjectAccessTests
{
    [Theory]
    [InlineData(AppRoles.Supervisor, true, false, true)]
    [InlineData(AppRoles.SiteManager, true, true, true)]
    [InlineData(AppRoles.CompanyAdmin, true, true, true)]
    [InlineData(AppRoles.SuperAdmin, true, true, true)]
    [InlineData(AppRoles.Storekeeper, false, false, false)]
    [InlineData(AppRoles.Employee, false, false, false)]
    public async Task Endpoint_authorization_matches_reporting_and_phase_responsibilities(
        string role, bool readPhase, bool writePhase, bool reporting)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(o => o.AddPolicy(AppPolicies.ProjectManagement,
            p => p.RequireRole(AppPolicies.ProjectManagementRoles)));
        using var provider = services.BuildServiceProvider();
        var actor = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "test"));
        foreach (var method in new[] { "GetAll", "GetById", "Create", "Update", "ChangeStatus" })
            Assert.Equal(method.StartsWith("Get") ? readPhase : writePhase,
                await Allowed(typeof(ProjectPhasesController), method));
        foreach (var method in new[] { "GetAll", "GetById", "Create", "Update" })
            Assert.Equal(reporting, await Allowed(typeof(DailyProgressLogsController), method));

        async Task<bool> Allowed(Type controller, string method)
        {
            var data = controller.GetCustomAttributes(true).OfType<IAuthorizeData>()
                .Concat(controller.GetMethod(method)!.GetCustomAttributes(true).OfType<IAuthorizeData>());
            var policy = await AuthorizationPolicy.CombineAsync(
                provider.GetRequiredService<IAuthorizationPolicyProvider>(), data);
            return (await provider.GetRequiredService<IAuthorizationService>()
                .AuthorizeAsync(actor, null, policy!)).Succeeded;
        }
    }

    [Fact]
    public async Task Supervisor_service_access_stays_in_company_and_cannot_create_phases()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var company = Guid.NewGuid();
        var actor = new User { Id = Guid.NewGuid(), CompanyId = company, IsActive = true,
            UserRoles = [new UserRole { Id = Guid.NewGuid(),
                Role = new Role { Id = Guid.NewGuid(), Name = AppRoles.Supervisor } }] };
        var own = new Project { Id = Guid.NewGuid(), CompanyId = company, Name = "Own" };
        var other = new Project { Id = Guid.NewGuid(), CompanyId = Guid.NewGuid(), Name = "Other" };
        db.Users.Add(actor);
        db.Projects.AddRange(own, other);
        db.SaveChanges(acceptAllChangesOnSuccess: true);
        var phases = new ProjectPhaseService(db);
        var logs = new DailyProgressLogService(db);
        Assert.Empty(await phases.GetAllAsync(actor.Id, own.Id));
        Assert.Empty(await logs.GetAllAsync(actor.Id, own.Id));
        await Assert.ThrowsAsync<ProjectPhaseManagementException>(() => phases.GetAllAsync(actor.Id, other.Id));
        await Assert.ThrowsAsync<DailyProgressLogManagementException>(() => logs.GetAllAsync(actor.Id, other.Id));
        await Assert.ThrowsAsync<ProjectPhaseManagementException>(() =>
            phases.CreateAsync(actor.Id, new() { ProjectId = own.Id, Name = "Denied", Sequence = 1 }));
    }

    [Theory]
    [InlineData(401, "Your session has expired. Please sign in again.")]
    [InlineData(403, "You do not have permission to view project phases.")]
    [InlineData(500, "Unable to load project phases. Please try again.")]
    [InlineData(0, "Unable to reach the project service. Please try again.")]
    public void Phase_load_failure_is_safe_and_does_not_show_management_controls(int status, string expected)
    {
        using var context = new TestContext();
        var proxy = DispatchProxy.Create<IProjectService, ProjectProxy>();
        ((ProjectProxy)(object)proxy).Status = status;
        context.Services.AddSingleton(proxy);
        var page = context.RenderComponent<ProjectPhases>(p => p.Add(x => x.ProjectId, Guid.NewGuid()));
        page.WaitForAssertion(() => Assert.Equal(expected, Assert.Single(page.FindAll("[role=alert]")).TextContent.Trim()));
        Assert.DoesNotContain("private error", page.Markup);
        Assert.DoesNotContain("Loading Work Breakdown", page.Markup);
        Assert.DoesNotContain("+ New Phase", page.Markup);
        Assert.DoesNotContain("No Phases Created", page.Markup);
        if (status is 500 or 0)
        {
            ((ProjectProxy)(object)proxy).Status = 200;
            page.FindAll("button").Single(x => x.TextContent.Contains("Retry")).Click();
            page.WaitForAssertion(() => Assert.Contains("No Phases Created", page.Markup));
        }
    }

    public class ProjectProxy : DispatchProxy
    {
        public int Status { get; set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            Status == 200 ? Task.FromResult(new List<ProjectPhase>()) :
            Task.FromException<List<ProjectPhase>>(new HttpRequestException(
                "private error", null, Status == 0 ? null : (HttpStatusCode)Status));
    }
}
