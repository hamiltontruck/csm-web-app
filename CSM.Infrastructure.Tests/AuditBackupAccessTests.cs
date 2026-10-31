using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using CSM.Api.Controllers;
using CSM.Domain.Entities.Audit;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class AuditBackupAccessTests
{
    [Fact]
    public void Audit_endpoint_requires_administrative_role_and_users_view()
    {
        var attributes = typeof(AuditController).GetCustomAttributes<AuthorizeAttribute>().ToArray();
        Assert.Contains(attributes, x => x.Policy == "Users.View");
        Assert.Contains(attributes, x => x.Policy == "UserManagement");
    }

    [Fact]
    public void Global_backup_endpoint_requires_super_admin()
    {
        Assert.Contains(typeof(BackupController).GetCustomAttributes<AuthorizeAttribute>(), x => x.Roles == "SuperAdmin");
    }

    [Fact]
    public async Task Future_audit_logs_capture_actor_company_at_write_time()
    {
        await using var db = Memory();
        var actor = AddActor(db, "CompanyAdmin", Guid.NewGuid());
        var originalCompany = actor.CompanyId;
        await new AuditService(db).LogAsync(actor.Id, "Actor", "Users", "Edit", "User", "1", "changed", null, default);
        actor.CompanyId = Guid.NewGuid();
        await db.SaveChangesAsync();
        Assert.Equal(originalCompany, (await db.AuditLogs.SingleAsync()).CompanyId);
    }

    [Theory]
    [InlineData("CompanyAdmin")]
    [InlineData("Administrator")]
    public async Task Company_audit_reader_excludes_foreign_and_legacy_logs(string role)
    {
        await using var db = Memory();
        var actor = AddActor(db, role, Guid.NewGuid());
        var own = new AuditLog { CompanyId = actor.CompanyId!.Value, Module = "Users" };
        db.AuditLogs.AddRange(own, new() { CompanyId = Guid.NewGuid(), Module = "Users" }, new() { CompanyId = Guid.Empty, UserId = actor.Id, Module = "Users" });
        await db.SaveChangesAsync();
        var logs = await Query(db, actor.Id);
        Assert.Equal(own.Id, Assert.Single(logs).Id);
    }

    [Theory]
    [InlineData("Supervisor", true, true)]
    [InlineData("CompanyAdmin", true, false)]
    [InlineData("SuperAdmin", false, true)]
    [InlineData("CompanyAdmin", true, true)]
    public async Task Audit_denies_missing_authority_or_company(string role, bool active, bool grant)
    {
        await using var db = Memory();
        var actor = AddActor(db, role, null, active, grant);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Query(db, actor.Id));
    }

    [Fact]
    public async Task PostgreSQL_audit_filters_and_super_admin_global_visibility()
    {
        var connection = new Npgsql.NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("CSM_TEST_POSTGRES") ?? "Host=localhost;Username=postgres;Password=postgres") { Database = "audit_" + Guid.NewGuid().ToString("N") };
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection.ConnectionString).Options);
        try
        {
            await db.Database.MigrateAsync();
            var company = new Company { Code = "AUD", Name = "Audit test", TimeZone = "UTC" };
            db.Companies.Add(company);
            var admin = AddActor(db, "CompanyAdmin", company.Id);
            var root = AddActor(db, "SuperAdmin", null);
            var now = DateTime.UtcNow;
            var deleted = new AuditLog { CompanyId = company.Id, Module = "Users", CreatedAtUtc = now };
            var matching = new AuditLog { CompanyId = company.Id, Module = "Users", CreatedAtUtc = now.AddMinutes(-1) };
            var old = new AuditLog { CompanyId = company.Id, Module = "Users", CreatedAtUtc = now.AddDays(-5) };
            var legacy = new AuditLog { CompanyId = Guid.Empty, Module = "Users", CreatedAtUtc = now };
            db.AuditLogs.AddRange(matching,
                new() { CompanyId = company.Id, Module = "Other", CreatedAtUtc = now },
                old, legacy,
                deleted);
            await db.SaveChangesAsync();
            db.AuditLogs.Remove(deleted);
            await db.SaveChangesAsync();
            await db.AuditLogs.Where(x => x.Id == matching.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAtUtc, now.AddMinutes(-1)));
            await db.AuditLogs.Where(x => x.Id == old.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAtUtc, now.AddDays(-5)));
            await db.AuditLogs.Where(x => x.Id == legacy.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAtUtc, now));
            var upper = DateTime.UtcNow;
            Assert.Equal(matching.Id, Assert.Single(await Query(db, admin.Id, "Users", now.AddHours(-1), upper)).Id);
            var global = await Query(db, root.Id, "Users", now.AddHours(-1), upper);
            Assert.Equal(2, global.Count);
            Assert.Equal(Guid.Empty, (await db.AuditLogs.SingleAsync(x => x.Id == global.First().Id)).CompanyId);
            await new AuditService(db).LogAsync(admin.Id, "Admin", "Write", "Create", "Record", "1", "created", null, default);
            Assert.Equal(company.Id, (await db.AuditLogs.SingleAsync(x => x.Module == "Write")).CompanyId);
            var backups = new BackupService(new ConfigurationBuilder().Build(), db);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => backups.EnsureAuthorizedAsync(admin.Id, default));
            await backups.EnsureAuthorizedAsync(root.Id, default);
            root.IsActive = false;
            await db.SaveChangesAsync();
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => backups.EnsureAuthorizedAsync(root.Id, default));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Query(db, root.Id));
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    [Theory]
    [InlineData("CompanyAdmin", true)]
    [InlineData("Administrator", true)]
    [InlineData("SuperAdmin", false)]
    [InlineData("Unknown", true)]
    public async Task Backup_denies_before_configuration_or_process_access(string role, bool active)
    {
        await using var db = Memory();
        var actor = AddActor(db, role, null, active);
        var config = new ConfigurationBuilder().Build();
        var service = new BackupService(config, db);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetBackupsAsync(actor.Id, default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CreateBackupAsync(actor.Id, default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RestoreBackupAsync(actor.Id, "/outside/never.dump", default));
    }


    [Fact]
    public async Task Demoted_actor_receives_forbidden_before_restore_upload_is_inspected()
    {
        await using var db = Memory();
        var actor = AddActor(db, "CompanyAdmin", Guid.NewGuid());
        var controller = new BackupController(new BackupService(new ConfigurationBuilder().Build(), db))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, actor.Id.ToString()), new Claim(ClaimTypes.Role, "SuperAdmin")], "test")) } }
        };
        Assert.IsType<ForbidResult>(await controller.RestoreBackup(new UnreadableUpload(), default));
        Assert.IsType<ForbidResult>((await controller.GetBackups(default)).Result);
        Assert.IsType<ForbidResult>((await controller.CreateBackup(default)).Result);
    }

    [Fact]
    public async Task Active_super_admin_can_list_local_backups_but_cannot_restore_outside_folder()
    {
        await using var db = Memory();
        var actor = AddActor(db, "SuperAdmin", null);
        var service = new BackupService(new ConfigurationBuilder().Build(), db);
        await service.EnsureAuthorizedAsync(actor.Id, default);
        Assert.NotNull(await service.GetBackupsAsync(actor.Id, default));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.RestoreBackupAsync(actor.Id, "/outside/never.dump", default));
        Assert.Contains("inside the backups folder", error.Message);
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("membership")]
    [InlineData("role")]
    public async Task Deleted_authority_cannot_read_audit_or_manage_backups(string deleted)
    {
        await using var db = Memory();
        var actor = AddActor(db, "SuperAdmin", null);
        if (deleted == "actor") actor.IsDeleted = true;
        if (deleted == "membership") actor.UserRoles.Single().IsDeleted = true;
        if (deleted == "role") actor.UserRoles.Single().Role.IsDeleted = true;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Query(db, actor.Id));
        var service = new BackupService(new ConfigurationBuilder().Build(), db);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetBackupsAsync(actor.Id, default));
    }

    [Fact]
    public async Task Unknown_actor_is_denied_and_system_log_has_no_inferred_company()
    {
        await using var db = Memory();
        var id = Guid.NewGuid();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Query(db, id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new BackupService(new ConfigurationBuilder().Build(), db).GetBackupsAsync(id, default));
        await new AuditService(db).LogAsync(id, "System", "System", "Run", "Job", "1", "completed", null, default);
        Assert.Equal(Guid.Empty, (await db.AuditLogs.SingleAsync()).CompanyId);
    }

    private sealed class UnreadableUpload : IFormFile
    {
        public string ContentType => throw new InvalidOperationException("Upload must not be inspected.");
        public string ContentDisposition => throw new InvalidOperationException("Upload must not be inspected.");
        public IHeaderDictionary Headers => throw new InvalidOperationException("Upload must not be inspected.");
        public long Length => throw new InvalidOperationException("Upload must not be inspected.");
        public string Name => throw new InvalidOperationException("Upload must not be inspected.");
        public string FileName => throw new InvalidOperationException("Upload must not be inspected.");
        public void CopyTo(Stream target) => throw new InvalidOperationException("Upload must not be read.");
        public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Upload must not be read.");
        public Stream OpenReadStream() => throw new InvalidOperationException("Upload must not be read.");
    }

    private static ApplicationDbContext Memory() => new MemoryContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private sealed class MemoryContext(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
    {
        public override int SaveChanges() => base.SaveChanges(true);
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => base.SaveChangesAsync(true, cancellationToken);
    }
    private static User AddActor(ApplicationDbContext db, string role, Guid? company, bool active = true, bool grant = true)
    {
        var actor = new User { CompanyId = company, IsActive = active, Email = Guid.NewGuid()+"@test.invalid", NormalizedEmail = Guid.NewGuid().ToString(), UserRoles = [new() { Role = new Role { Name = role, NormalizedName = Guid.NewGuid().ToString(), GranularPermissions = grant ? [new() { Permission = GranularPermission.Users_View }] : [] } }] };
        db.Users.Add(actor); db.SaveChanges(); return actor;
    }
    private static Task<IReadOnlyCollection<CSM.Application.Common.Audit.AuditLogDto>> Query(ApplicationDbContext db, Guid actorId, string? module = null, DateTime? from = null, DateTime? to = null)
        => new AuditQueryService(db).GetLogsAsync(actorId, module, from, to, default);
}
