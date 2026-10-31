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
    [Fact]
    public async Task PostgreSQL_enforces_assigned_scope_and_retains_authorized_writes()
    {
        var connection = new Npgsql.NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("CSM_TEST_POSTGRES") ?? "Host=localhost;Username=postgres;Password=postgres") { Database = "compliance_authority_" + Guid.NewGuid().ToString("N") };
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection.ConnectionString).Options);
        try
        {
            await db.Database.MigrateAsync();
            var company = new Company { Code = "OWN", Name = "Own" }; var otherCompany = new Company { Code = "OTHER", Name = "Other" };
            var site = new ConstructionSite { CompanyId = company.Id, SiteCode = "ONE", Name = "Assigned" }; var unassigned = new ConstructionSite { CompanyId = company.Id, SiteCode = "TWO", Name = "Unassigned" }; var foreign = new ConstructionSite { CompanyId = otherCompany.Id, SiteCode = "THREE", Name = "Foreign" };
            User Actor(string role, Guid? companyId) => new() { CompanyId = companyId, Email = role + "@example.test", NormalizedEmail = role.ToUpperInvariant() + "@EXAMPLE.TEST", IsActive = true, UserRoles = [new() { Role = new Role { Name = role, NormalizedName = role.ToUpperInvariant(), GranularPermissions = [new() { Permission = GranularPermission.Compliance_View }, new() { Permission = GranularPermission.Compliance_Manage }] } }] };
            var manager = Actor("SiteManager", company.Id); var admin = Actor("CompanyAdmin", company.Id); var global = Actor("SuperAdmin", null);
            db.Companies.AddRange(company, otherCompany); db.ConstructionSites.AddRange(site, unassigned, foreign); db.Users.AddRange(manager, admin, global);
            db.UserSiteAccesses.Add(new() { CompanyId = company.Id, UserId = manager.Id, SiteId = site.Id, IsActive = true });
            Permit Permit(ConstructionSite at, string code) => new() { CompanyId = at.CompanyId, ConstructionSiteId = at.Id, PermitNumber = code, PermitType = "Test", Name = code };
            Inspection Inspection(ConstructionSite at, string code) => new() { CompanyId = at.CompanyId, ConstructionSiteId = at.Id, InspectionNumber = code, InspectionType = "Test", Title = code, ScheduledAtUtc = DateTime.UtcNow };
            var deniedPermit = Permit(unassigned, "TWO"); var deniedInspection = Inspection(unassigned, "TWO");
            db.Permits.AddRange(Permit(site, "ONE"), deniedPermit, Permit(foreign, "THREE"));
            db.Inspections.AddRange(Inspection(site, "ONE"), deniedInspection, Inspection(foreign, "THREE"));
            await db.SaveChangesAsync();
            var permits = new PermitService(db); var inspections = new InspectionService(db);
            Assert.Single(await permits.GetAllAsync(manager.Id)); Assert.Single(await inspections.GetAllAsync(manager.Id));
            Assert.Equal(2, (await permits.GetAllAsync(admin.Id)).Count); Assert.Equal(2, (await inspections.GetAllAsync(admin.Id)).Count);
            Assert.Equal(3, (await permits.GetAllAsync(global.Id)).Count); Assert.Equal(3, (await inspections.GetAllAsync(global.Id)).Count);
            await Assert.ThrowsAsync<KeyNotFoundException>(() => permits.GetByIdAsync(manager.Id, deniedPermit.Id));
            await Assert.ThrowsAsync<KeyNotFoundException>(() => inspections.GetByIdAsync(manager.Id, deniedInspection.Id));
            CreatePermitRequest NewPermit(ConstructionSite at, string code) => new() { ConstructionSiteId = at.Id, PermitNumber = code, PermitType = "Test", Name = code };
            CreateInspectionRequest NewInspection(ConstructionSite at, string code) => new() { ConstructionSiteId = at.Id, InspectionNumber = code, InspectionType = "Test", Title = code, ScheduledAtUtc = DateTime.UtcNow };
            Assert.Equal(company.Id, (await permits.CreateAsync(manager.Id, NewPermit(site, "NEW"))).CompanyId);
            Assert.Equal(company.Id, (await inspections.CreateAsync(manager.Id, NewInspection(site, "NEW"))).CompanyId);
            await Assert.ThrowsAsync<KeyNotFoundException>(() => permits.CreateAsync(manager.Id, NewPermit(unassigned, "DENIED")));
            await Assert.ThrowsAsync<KeyNotFoundException>(() => inspections.CreateAsync(manager.Id, NewInspection(unassigned, "DENIED")));
            Assert.Equal(otherCompany.Id, (await permits.CreateAsync(global.Id, NewPermit(foreign, "GLOBAL"))).CompanyId);
            Assert.Equal(otherCompany.Id, (await inspections.CreateAsync(global.Id, NewInspection(foreign, "GLOBAL"))).CompanyId);
            var grant = await db.UserSiteAccesses.SingleAsync(x => x.UserId == manager.Id); grant.IsActive = false; await db.SaveChangesAsync();
            Assert.Empty(await permits.GetAllAsync(manager.Id)); Assert.Empty(await inspections.GetAllAsync(manager.Id));
        }
        finally { await db.Database.EnsureDeletedAsync(); }
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
