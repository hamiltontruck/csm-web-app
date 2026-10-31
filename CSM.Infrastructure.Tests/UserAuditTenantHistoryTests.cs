using CSM.Domain.Constants;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Security;
using CSM.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class UserAuditTenantHistoryTests
{
    [Fact]
    public async Task Unknown_legacy_audit_rows_are_only_visible_to_SuperAdmin()
    {
        using var f = new Fixture();
        f.Db.UserAudits.Add(new UserAudit { ActorUser = f.Admin, TargetUser = f.Target, Action = "legacy without a tenant" });
        f.Db.SaveChanges(true);

        Assert.Empty(await f.Service.GetAuditAsync(f.Admin.Id));
        Assert.Equal("legacy without a tenant", Assert.Single(await f.Service.GetAuditAsync(f.SuperAdmin.Id)).Action);
    }

    [Fact]
    public async Task Moving_both_users_to_another_company_does_not_reattribute_old_audit()
    {
        using var f = new Fixture();
        var history = new UserAudit { ActorUser = f.Admin, TargetUser = f.Target, Action = "company A history" };
        SetRecordedCompanyIfAvailable(history, f.CompanyA);
        f.Db.UserAudits.Add(history);
        f.Db.SaveChanges(true);

        f.Admin.CompanyId = f.CompanyB;
        f.Target.CompanyId = f.CompanyB;
        f.Db.SaveChanges(true);

        Assert.Empty(await f.Service.GetAuditAsync(f.Admin.Id));
        Assert.Equal("company A history", Assert.Single(await f.Service.GetAuditAsync(f.SuperAdmin.Id)).Action);
    }

    [Fact]
    public async Task New_audit_records_the_target_tenant_and_keeps_it_after_both_users_transfer()
    {
        using var f = new Fixture();
        await f.Service.ActivateAsync(f.Admin.Id, f.Target.Id);
        var history = Assert.Single(f.Db.UserAudits);
        var companyProperty = typeof(UserAudit).GetProperty("CompanyId");
        Assert.NotNull(companyProperty);
        Assert.Equal(f.CompanyA, Assert.IsType<Guid>(companyProperty.GetValue(history)));
        Assert.Single(await f.Service.GetAuditAsync(f.Admin.Id));

        f.Admin.CompanyId = f.CompanyB;
        f.Target.CompanyId = f.CompanyB;
        f.Db.SaveChanges(true);

        Assert.Equal(f.CompanyA, Assert.IsType<Guid>(companyProperty.GetValue(history)));
        Assert.Empty(await f.Service.GetAuditAsync(f.Admin.Id));
        Assert.Single(await f.Service.GetAuditAsync(f.SuperAdmin.Id));
    }

    // Keep these regressions compilable before the tenant snapshot schema exists.
    private static void SetRecordedCompanyIfAvailable(UserAudit audit, Guid companyId) =>
        typeof(UserAudit).GetProperty("CompanyId")?.SetValue(audit, companyId);

    private sealed class Fixture : IDisposable
    {
        public TestDb Db { get; } = new();
        public Guid CompanyA { get; } = Guid.NewGuid();
        public Guid CompanyB { get; } = Guid.NewGuid();
        public User Admin { get; }
        public User Target { get; }
        public User SuperAdmin { get; }
        public UserService Service { get; }

        public Fixture()
        {
            Admin = new User { CompanyId = CompanyA, IsActive = true, UserRoles = [new() { Role = new Role {
                Name = AppRoles.CompanyAdmin, GranularPermissions = [
                    new() { Permission = GranularPermission.Users_View },
                    new() { Permission = GranularPermission.Users_Edit }
                ]
            } }] };
            Target = new User { CompanyId = CompanyA, IsActive = true };
            SuperAdmin = new User { IsActive = true, UserRoles = [new() { Role = new Role {
                Name = AppRoles.SuperAdmin, GranularPermissions = [new() { Permission = GranularPermission.Users_View }]
            } }] };
            Db.Users.AddRange(Admin, Target, SuperAdmin);
            Db.SaveChanges(true);
            Service = new UserService(Db, new PasswordHasher());
        }

        public void Dispose() => Db.Dispose();
    }

    public sealed class TestDb : ApplicationDbContext
    {
        public TestDb() : base(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options) { }
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => base.SaveChangesAsync(true, cancellationToken);
    }
}
