using System.Security.Claims;
using CSM.Api.Controllers;
using CSM.Application.UserPreferences.Dtos;
using CSM.Domain.Entities.Identity;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Services;
using CSM.Infrastructure.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class SettingsPreferencesTests
{
    private static readonly UpdateUserPreferencesRequest Valid = new("System", "en", "UTC", "dd/MM/yyyy", "24h", "en-US",
        false, true, true, true, true, true, true, true, true, true, true, true, false, false);
    private static DbContextOptions<ApplicationDbContext> Options() => new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

    [Theory]
    [InlineData("Language", "zz")]
    [InlineData("DateFormat", "garbage")]
    [InlineData("TimeZone", "not-a-time-zone")]
    [InlineData("NumberFormat", "not-a-culture")]
    public async Task Invalid_preference_is_rejected_without_overwriting_saved_values(string field, string value)
    {
        var options = Options(); Guid id;
        using (var db = new TestDb(options))
        {
            var actor = new User(); db.Users.Add(actor); await db.SaveChangesAsync(); id = actor.Id;
            var service = new UserPreferencesService(db); await service.UpdateAsync(id, Valid);
            var invalid = field switch
            {
                "Language" => Valid with { Language = value }, "DateFormat" => Valid with { DateFormat = value },
                "TimeZone" => Valid with { TimeZone = value }, _ => Valid with { NumberFormat = value }
            };
            await Assert.ThrowsAsync<CSM.Application.Common.Exceptions.UserManagementException>(() => service.UpdateAsync(id, invalid));
        }
        using var fresh = new TestDb(options);
        var saved = await new UserPreferencesService(fresh).GetAsync(id);
        Assert.Equal("en", saved.Language); Assert.Equal("dd/MM/yyyy", saved.DateFormat); Assert.Equal("UTC", saved.TimeZone); Assert.Equal("en-US", saved.NumberFormat);
    }

    [Fact]
    public async Task Preferences_roundtrip_belongs_only_to_the_current_active_user()
    {
        var options = Options(); Guid id; Guid other;
        using (var db = new TestDb(options))
        {
            var actor = new User(); var foreign = new User(); db.Users.AddRange(actor, foreign); await db.SaveChangesAsync(); id = actor.Id; other = foreign.Id;
            await new UserPreferencesService(db).UpdateAsync(id, Valid with { Theme = "Dark", CompactMode = true, Language = "om", AnalyticsConsent = true });
        }
        using var fresh = new TestDb(options); var service = new UserPreferencesService(fresh);
        var own = await service.GetAsync(id); var foreignSaved = await service.GetAsync(other);
        Assert.Equal("Dark", own.Theme); Assert.True(own.CompactMode); Assert.True(own.AnalyticsConsent); Assert.Equal("om", own.Language);
        Assert.Equal("System", foreignSaved.Theme); Assert.False(foreignSaved.CompactMode);
        (await fresh.Users.SingleAsync(x => x.Id == id)).IsActive = false; await fresh.SaveChangesAsync();
        await Assert.ThrowsAsync<CSM.Application.Common.Exceptions.UserManagementException>(() => service.GetAsync(id));
    }

    [Fact]
    public async Task Self_export_works_without_administrative_export_grants_and_excludes_secrets()
    {
        using var db = new TestDb(Options()); var actor = new User { Email = "own@example.test", PasswordHash = "SECRET-HASH" };
        db.Users.Add(actor); await db.SaveChangesAsync();
        var controller = new PersonalDataController(db, new NoPermissions()) { ControllerContext = Context(actor.Id) };
        var file = Assert.IsType<FileContentResult>(await controller.Export(default));
        var json = System.Text.Encoding.UTF8.GetString(file.FileContents);
        Assert.Contains("own@example.test", json); Assert.DoesNotContain("SECRET-HASH", json);
        Assert.DoesNotContain("PasswordHash", json); Assert.DoesNotContain("TokenHash", json);
    }

    private static ControllerContext Context(Guid id) => new() { HttpContext = new DefaultHttpContext {
        User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.ToString())], "test")) } };

    [Fact]
    public async Task Inactive_or_foreign_company_claim_cannot_export_or_read_privacy_requests()
    {
        using var db = new TestDb(Options()); var actor = new User { CompanyId = Guid.NewGuid(), IsActive = true };
        db.Users.Add(actor); await db.SaveChangesAsync();
        var context = Context(actor.Id);
        ((ClaimsIdentity)context.HttpContext.User.Identity!).AddClaim(new Claim("company_id", Guid.NewGuid().ToString()));
        var export = new PersonalDataController(db, new NoPermissions()) { ControllerContext = context };
        Assert.IsType<ForbidResult>(await export.Export(default));
        var privacy = new PrivacyDataRequestsController(db) { ControllerContext = context };
        Assert.IsType<UnauthorizedResult>(await privacy.GetMine(default));
        actor.IsActive = false; await db.SaveChangesAsync();
        export.ControllerContext = Context(actor.Id);
        Assert.IsType<UnauthorizedResult>(await export.Export(default));
    }

    [Fact]
    public async Task Deletion_request_is_own_company_submission_and_does_not_delete_the_account()
    {
        using var db = new TestDb(Options()); var actor = new User { CompanyId = Guid.NewGuid() }; db.Users.Add(actor); await db.SaveChangesAsync();
        var context = Context(actor.Id); ((ClaimsIdentity)context.HttpContext.User.Identity!).AddClaim(new Claim("company_id", actor.CompanyId.ToString()!));
        var controller = new PrivacyDataRequestsController(db) { ControllerContext = context };
        Assert.IsType<CreatedAtActionResult>(await controller.Create(new() { RequestType = "DataDeletion", Description = "Please review my request." }, default));
        var request = Assert.Single(await db.Set<PrivacyDataRequest>().ToListAsync());
        Assert.Equal(actor.Id, request.UserId); Assert.Equal(actor.CompanyId, request.CompanyId); Assert.Equal("Submitted", request.Status);
        db.ChangeTracker.Clear(); Assert.False((await db.Users.SingleAsync()).IsDeleted);
    }
    private sealed class NoPermissions : IPermissionEvaluator
    {
        public Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken ct = default) => Task.FromResult(false);
        public Task<IReadOnlyCollection<string>> GetPermissionsAsync(Guid userId, CancellationToken ct = default) => Task.FromResult<IReadOnlyCollection<string>>([]);
    }
    private sealed class TestDb(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
    {
        public override Task<int> SaveChangesAsync(CancellationToken ct = default) => base.SaveChangesAsync(true, ct);
    }
}
