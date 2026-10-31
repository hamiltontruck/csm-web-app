using CSM.Domain.Constants;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
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

    [Fact]
    public async Task Deletion_records_the_targets_company_and_masks_its_deleted_identity()
    {
        using var f = new Fixture();
        f.Db.RoleGranularPermissions.Add(new() { RoleId = f.Admin.UserRoles.Single().Role.Id, Permission = GranularPermission.Users_Delete });
        f.Db.SaveChanges(true);
        await f.Service.DeleteAsync(f.Admin.Id, f.Target.Id);
        Assert.Equal(f.CompanyA, Assert.Single(f.Db.UserAudits).CompanyId);
        Assert.Equal("Unavailable user", Assert.Single(await f.Service.GetAuditAsync(f.Admin.Id)).TargetName);
    }

    [Fact]
    public async Task Tenant_snapshot_survives_migration_and_masks_transferred_users_in_PostgreSQL()
    {
        var connection = new Npgsql.NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("CSM_TEST_POSTGRES") ?? "Host=localhost;Username=postgres;Password=postgres") {
            Database = "user_audit_" + Guid.NewGuid().ToString("N")
        };
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection.ConnectionString).Options);
        try
        {
            await db.GetService<IMigrator>().MigrateAsync("20261016161037_InitialPostgres");
            var companyA = new Company { Code = "AUDITA", Name = "Audit A" };
            var companyB = new Company { Code = "AUDITB", Name = "Audit B" };
            var role = new Role { Name = AppRoles.CompanyAdmin, NormalizedName = "COMPANYADMIN", GranularPermissions = [
                new() { Permission = GranularPermission.Users_View }, new() { Permission = GranularPermission.Users_Edit }
            ] };
            var admin = new User { CompanyId = companyA.Id, Email = "admin@audit.test", NormalizedEmail = "ADMIN@AUDIT.TEST", FirstName = "Current", LastName = "Admin", IsActive = true, UserRoles = [new() { Role = role }] };
            var target = new User { CompanyId = companyA.Id, Email = "target@audit.test", NormalizedEmail = "TARGET@AUDIT.TEST", FirstName = "Current", LastName = "Target", IsActive = true };
            var reader = new User { CompanyId = companyA.Id, Email = "reader@audit.test", NormalizedEmail = "READER@AUDIT.TEST", IsActive = true, UserRoles = [new() { Role = role }] };
            db.Companies.AddRange(companyA, companyB); db.Users.AddRange(admin, target, reader);
            await db.SaveChangesAsync();
            var legacyId = Guid.NewGuid();
            var performed = DateTime.UtcNow;
            var legacyAction = "legacy";
            var legacyDetails = "unknown tenant";
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"UserAudits\" (\"Id\", \"ActorUserId\", \"TargetUserId\", \"Action\", \"Details\", \"PerformedAtUtc\") VALUES ({legacyId}, {admin.Id}, {target.Id}, {legacyAction}, {legacyDetails}, {performed})");
            await db.Database.MigrateAsync();
            Assert.Null((await db.UserAudits.SingleAsync()).CompanyId);
            var service = new UserService(db, new PasswordHasher());
            Assert.Empty(await service.GetAuditAsync(admin.Id));
            await service.ActivateAsync(admin.Id, target.Id);
            Assert.Equal(companyA.Id, (await db.UserAudits.SingleAsync(x => x.Id != legacyId)).CompanyId);
            Assert.Single(await service.GetAuditAsync(admin.Id));
            admin.CompanyId = companyB.Id; target.CompanyId = companyB.Id;
            await db.SaveChangesAsync();
            Assert.Empty(await service.GetAuditAsync(admin.Id));
            var historical = Assert.Single(await service.GetAuditAsync(reader.Id));
            Assert.Equal("Unavailable user", historical.ActorName);
            Assert.Equal("Unavailable user", historical.TargetName);
            Assert.Equal(2, await db.UserAudits.CountAsync());
            Assert.Null((await db.UserAudits.SingleAsync(x => x.Id == legacyId)).CompanyId);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
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
