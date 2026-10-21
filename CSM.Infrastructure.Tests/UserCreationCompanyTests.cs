using CSM.Application.Common.Exceptions;
using CSM.Application.Common.Security;
using CSM.Application.Users.Dtos;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Entities.HRM;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class UserCreationCompanyTests
{
    [Theory]
    [InlineData(AppRoles.SuperAdmin)]
    [InlineData(AppRoles.CompanyAdmin)]
    public async Task Creates_company_user_with_requested_site_default_and_profile(string actorRole)
    {
        using var db = Database(actorRole);
        var result = await new UserService(db, new Hasher()).CreateAsync(ActorId, Request());
        var saved = await db.Users.Include(x => x.SiteAccesses).SingleAsync(x => x.Id == result.Id);
        Assert.Equal(CompanyId, saved.CompanyId);
        Assert.Equal(SiteId, saved.DefaultSiteId);
        Assert.Equal("Test phone", saved.PhoneNumber);
        Assert.Equal("Storekeeper", saved.JobTitle);
        var access = Assert.Single(saved.SiteAccesses);
        Assert.Equal(SiteId, access.SiteId);
        Assert.Equal(CompanyId, access.CompanyId);
        Assert.True(access.IsDefault);
        Assert.True(access.IsActive);
        Assert.True(saved.MustChangePassword);
        Assert.NotEqual(Request().TemporaryPassword, saved.PasswordHash);
        Assert.Contains("Storekeeper", result.Roles);
        var visible = await new UserService(db, new Hasher()).GetAllAsync(ActorId);
        Assert.Contains(visible, x => x.Id == saved.Id);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("inactive")]
    [InlineData("deleted")]
    public async Task Rejects_unusable_company_without_creating_user(string reason)
    {
        using var db = Database(AppRoles.SuperAdmin);
        var request = Request();
        if (reason == "missing") request = request with { CompanyId = Guid.NewGuid() };
        if (reason == "inactive") db.Companies.Single().IsActive = false;
        if (reason == "deleted") db.Companies.Single().IsDeleted = true;
        db.SaveChanges(acceptAllChangesOnSuccess: true);
        await Assert.ThrowsAsync<UserManagementException>(() =>
            new UserService(db, new Hasher()).CreateAsync(ActorId, request));
        Assert.Single(db.Users);
        Assert.Empty(db.UserSiteAccesses);
    }

    [Theory]
    [InlineData("foreign")]
    [InlineData("missing")]
    [InlineData("inactive")]
    [InlineData("default-not-assigned")]
    public async Task Rejects_sites_outside_selected_company_or_invalid_default(string reason)
    {
        using var db = Database(AppRoles.SuperAdmin);
        var request = Request();
        if (reason == "foreign") db.ConstructionSites.Single().CompanyId = Guid.NewGuid();
        if (reason == "inactive") db.ConstructionSites.Single().IsActive = false;
        if (reason == "missing") request = request with { SiteIds = [Guid.NewGuid()], DefaultSiteId = null };
        if (reason == "default-not-assigned") request = request with { DefaultSiteId = Guid.NewGuid() };
        db.SaveChanges(acceptAllChangesOnSuccess: true);
        await Assert.ThrowsAsync<UserManagementException>(() =>
            new UserService(db, new Hasher()).CreateAsync(ActorId, request));
        Assert.Single(db.Users);
        Assert.Empty(db.UserSiteAccesses);
    }

    [Fact]
    public async Task Rejects_employee_from_another_company()
    {
        using var db = Database(AppRoles.SuperAdmin);
        var employee = new Employee { Id = Guid.NewGuid(), CompanyId = Guid.NewGuid(), EmployeeNumber = "EMP-OTHER" };
        db.Employees.Add(employee);
        db.SaveChanges(acceptAllChangesOnSuccess: true);
        await Assert.ThrowsAsync<UserManagementException>(() => new UserService(db, new Hasher()).CreateAsync(
            ActorId, Request() with { EmployeeId = employee.Id }));
        Assert.Single(db.Users);
    }

    [Fact]
    public async Task Company_admin_cannot_forge_other_company_or_assign_superadmin()
    {
        using var db = Database(AppRoles.CompanyAdmin);
        var service = new UserService(db, new Hasher());
        await Assert.ThrowsAsync<UserManagementException>(() => service.CreateAsync(
            ActorId, Request() with { CompanyId = Guid.NewGuid() }));
        await Assert.ThrowsAsync<UserManagementException>(() => service.CreateAsync(
            ActorId, Request() with { Roles = [AppRoles.SuperAdmin], CompanyId = null }));
        Assert.Single(db.Users);
    }

    [Fact]
    public async Task Employee_cannot_create_users_even_in_own_company()
    {
        using var db = Database(AppRoles.Employee);
        await Assert.ThrowsAsync<UserManagementException>(() =>
            new UserService(db, new Hasher()).CreateAsync(ActorId, Request()));
        Assert.Single(db.Users);
    }

    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid SiteId = Guid.NewGuid();

    private static CreateUserRequest Request() => new(
        CompanyId, null, "storekeeper@example.test", "Test", "Storekeeper", "Example-Password123!",
        "Test phone", "Storekeeper", SiteId, [AppRoles.Storekeeper], [SiteId]);

    private static TestDatabase Database(string actorRole)
    {
        var db = new TestDatabase(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Companies.Add(new Company { Id = CompanyId, Code = "HBG", Name = "Hamilton Business Group", IsActive = true });
        db.ConstructionSites.Add(new ConstructionSite { Id = SiteId, CompanyId = CompanyId, Name = "Hamilton Test Site" });
        var role = new Role { Id = Guid.NewGuid(), Name = actorRole, NormalizedName = actorRole.ToUpperInvariant() };
        db.Users.Add(new User { Id = ActorId, IsActive = true, CompanyId = actorRole == AppRoles.SuperAdmin ? null : CompanyId,
            UserRoles = [new UserRole { Role = role }] });
        db.Roles.Add(new Role { Id = Guid.NewGuid(), Name = AppRoles.Storekeeper, NormalizedName = "STOREKEEPER" });
        db.SaveChanges(acceptAllChangesOnSuccess: true);
        return db;
    }

    private sealed class TestDatabase(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
    {
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            base.SaveChangesAsync(acceptAllChangesOnSuccess: true, cancellationToken);
    }

    private sealed class Hasher : IPasswordHasher
    {
        public string Hash(string password) => "hashed-for-test";
        public bool Verify(string password, string passwordHash) => throw new NotSupportedException();
    }
}
