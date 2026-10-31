using System.Reflection;
using System.Security.Claims;
using CSM.Application.Common.Security;
using CSM.Application.Compliance.Permits.Dtos;
using CSM.Application.Compliance.Inspections.Dtos;
using CSM.Domain.Entities.Compliance;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace CSM.Infrastructure.Tests;
public sealed class ComplianceManagementAuthorityTests
{
    public static IEnumerable<object[]> Actions
    {
        get
        {
            foreach (var controller in new[] { "PermitsController", "InspectionsController" })
                foreach (var action in new[] { "GetAll", "GetById", "Create", "Update" })
                    yield return [controller, action, action is "Create" or "Update" ? GranularPermission.Compliance_Manage : GranularPermission.Compliance_View, GranularPermission.Compliance_View];
        }
    }
    [Theory, MemberData(nameof(Actions))]
    public Task Actions_require_current_granular_grant(string controller, string action, GranularPermission required, GranularPermission read)
        => new BackendActionPermissionTests().HTTP_action_requires_its_own_permission_even_when_all_role_checks_pass(controller, action, required, read);
    [Theory]
    [InlineData("SuperAdmin", true)]
    [InlineData("CompanyAdmin", true)]
    [InlineData("SiteManager", true)]
    [InlineData("Administrator", false)]
    [InlineData("ProcurementOfficer", false)]
    [InlineData("Storekeeper", false)]
    [InlineData("Supervisor", false)]
    public async Task HTTP_roles_match_existing_service_contract(string role, bool allowed)
    {
        var registration = new ServiceCollection().AddLogging().AddGrantedBackendPermissions(role, [GranularPermission.Compliance_View]);
        registration.AddAuthorization(o => o.AddPolicy(AppPolicies.ProcurementManagement, p => p.RequireRole(AppPolicies.ProcurementManagementRoles)));
        using var services = registration.BuildServiceProvider();
        var actor = BackendAuthorizationFixture.WithGrantedIdentity(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "test")));
        foreach (var controller in new[] { typeof(CSM.Api.Controllers.PermitsController), typeof(CSM.Api.Controllers.InspectionsController) })
        {
            var data = controller.GetCustomAttributes<AuthorizeAttribute>().Concat(controller.GetMethod("GetAll")!.GetCustomAttributes<AuthorizeAttribute>());
            var policy = await AuthorizationPolicy.CombineAsync(services.GetRequiredService<IAuthorizationPolicyProvider>(), data);
            Assert.Equal(allowed, (await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(actor, null, policy!)).Succeeded);
        }
    }
    [Theory]
    [InlineData("permit")]
    [InlineData("inspection")]
    public async Task Site_manager_reads_only_assigned_site_and_cannot_open_other_site(string kind)
    {
        using var f = new Fixture("SiteManager");
        Assert.Single(await Read(f, kind));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Detail(f, kind, kind == "permit" ? f.UnassignedPermit.Id : f.UnassignedInspection.Id));
    }
    [Theory]
    [InlineData("permit")]
    [InlineData("inspection")]
    public async Task Missing_view_or_disabled_actor_denies_direct_service(string kind)
    {
        using var f = new Fixture("SiteManager");
        f.Actor.IsActive = false; f.Db.SaveChanges(true);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Read(f, kind));
        f.Actor.IsActive = true;
        f.Db.RoleGranularPermissions.RemoveRange(f.Db.RoleGranularPermissions.Where(x => x.Permission == GranularPermission.Compliance_View)); f.Db.SaveChanges(true);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Read(f, kind));
    }
    [Theory]
    [InlineData("permit")]
    [InlineData("inspection")]
    public async Task View_only_actor_cannot_create_records(string kind)
    {
        using var f = new Fixture("SiteManager");
        f.Db.RoleGranularPermissions.RemoveRange(f.Db.RoleGranularPermissions.Where(x => x.Permission == GranularPermission.Compliance_Manage)); f.Db.SaveChanges(true);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => kind == "permit"
            ? (Task)new PermitService(f.Db).CreateAsync(f.Actor.Id, new CreatePermitRequest())
            : new InspectionService(f.Db).CreateAsync(f.Actor.Id, new CreateInspectionRequest()));
    }
    [Theory]
    [InlineData("CompanyAdmin", 2)]
    [InlineData("SuperAdmin", 3)]
    public async Task Company_and_global_admin_keep_their_existing_scope(string role, int count)
    {
        using var f = new Fixture(role);
        Assert.Equal(count, (await Read(f, "permit")).Length);
        Assert.Equal(count, (await Read(f, "inspection")).Length);
    }
    private static async Task<Guid[]> Read(Fixture f, string kind) => kind == "permit"
        ? (await new PermitService(f.Db).GetAllAsync(f.Actor.Id)).Select(x => x.Id).ToArray()
        : (await new InspectionService(f.Db).GetAllAsync(f.Actor.Id)).Select(x => x.Id).ToArray();
    private static Task Detail(Fixture f, string kind, Guid id) => kind == "permit"
        ? new PermitService(f.Db).GetByIdAsync(f.Actor.Id, id)
        : new InspectionService(f.Db).GetByIdAsync(f.Actor.Id, id);
    private sealed class Fixture : IDisposable
    {
        public TestDb Db = new();
        public User Actor;
        public Permit UnassignedPermit;
        public Inspection UnassignedInspection;
        public Fixture(string role)
        {
            var company = Guid.NewGuid(); var foreign = Guid.NewGuid();
            Actor = new User { CompanyId = role == "SuperAdmin" ? null : company, IsActive = true, UserRoles = [new() { Role = new Role { Name = role, GranularPermissions = [new() { Permission = GranularPermission.Compliance_View }, new() { Permission = GranularPermission.Compliance_Manage }] } }] };
            var first = new ConstructionSite { CompanyId = company, Name = "Assigned" }; var other = new ConstructionSite { CompanyId = company, Name = "Unassigned" }; var elsewhere = new ConstructionSite { CompanyId = foreign, Name = "Foreign" };
            Db.Users.Add(Actor); Db.ConstructionSites.AddRange(first, other, elsewhere);
            Db.UserSiteAccesses.Add(new() { CompanyId = company, UserId = Actor.Id, SiteId = first.Id, IsActive = true });
            UnassignedPermit = new Permit { CompanyId = company, ConstructionSiteId = other.Id };
            UnassignedInspection = new Inspection { CompanyId = company, ConstructionSiteId = other.Id };
            Db.Permits.AddRange(new Permit { CompanyId = company, ConstructionSiteId = first.Id }, UnassignedPermit, new Permit { CompanyId = foreign, ConstructionSiteId = elsewhere.Id });
            Db.Inspections.AddRange(new Inspection { CompanyId = company, ConstructionSiteId = first.Id }, UnassignedInspection, new Inspection { CompanyId = foreign, ConstructionSiteId = elsewhere.Id });
            Db.SaveChanges(true);
        }
        public void Dispose() => Db.Dispose();
    }
    public sealed class TestDb : ApplicationDbContext
    {
        public TestDb() : base(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options) { }
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => base.SaveChangesAsync(true, cancellationToken);
    }
}
