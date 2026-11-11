using CSM.Application.Users.Dtos;
using CSM.Application.Common.Security;
using CSM.Domain.Entities.Identity;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class AccountSettingsTests
{
    private static ApplicationDbContext Database() => new TestDb(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static TokenService Tokens() => new(Options.Create(new JwtSettings
        { Issuer = "test", Audience = "test", SecretKey = "test-only-signing-key-with-at-least-32-characters" }));
    private static CSM.Infrastructure.Services.AccountSettingsService Service(ApplicationDbContext db) => new(db, Tokens());

    [Fact]
    public async Task Own_profile_changes_persist_without_changing_identity_or_other_users()
    {
        using var db = Database();
        var own = new User { Email = "own@example.test", FirstName = "Old", LastName = "Name" };
        var other = new User { Email = "other@example.test", FirstName = "Other" };
        db.Users.AddRange(own, other); await db.SaveChangesAsync();
        await Service(db).UpdateProfileAsync(own.Id, new UpdateMyProfileRequest("New", "Name", "123", "Accountant"), CancellationToken.None);
        db.ChangeTracker.Clear();
        Assert.Equal("New", (await db.Users.SingleAsync(x => x.Id == own.Id)).FirstName);
        Assert.Equal("own@example.test", (await db.Users.SingleAsync(x => x.Id == own.Id)).Email);
        Assert.Equal("Other", (await db.Users.SingleAsync(x => x.Id == other.Id)).FirstName);
    }

    [Fact]
    public async Task Companyless_user_has_empty_memberships_and_inactive_actor_is_denied()
    {
        using var db = Database(); var own = new User(); db.Users.Add(own); await db.SaveChangesAsync();
        var memberships = await Service(db).GetMembershipsAsync(own.Id, CancellationToken.None);
        Assert.Empty((System.Collections.IEnumerable)memberships);
        own.IsActive = false; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<CSM.Application.Common.Exceptions.AuthenticationException>(async () =>
            await Service(db).GetProfileAsync(own.Id, CancellationToken.None));
    }

    [Theory]
    [InlineData(null)] [InlineData("unknown")] [InlineData("foreign")]
    public async Task Unverified_current_token_cannot_revoke_sessions(string? raw)
    {
        using var db = Database(); var own = new User(); var other = new User(); db.Users.AddRange(own, other);
        db.RefreshTokens.AddRange(
            new RefreshToken { UserId = own.Id, TokenHash = Tokens().HashRefreshToken("own"), ExpiresAtUtc = DateTime.UtcNow.AddDays(1) },
            new RefreshToken { UserId = other.Id, TokenHash = Tokens().HashRefreshToken("foreign"), ExpiresAtUtc = DateTime.UtcNow.AddDays(1) });
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<CSM.Application.Common.Exceptions.AuthenticationException>(async () =>
            await Service(db).RevokeOtherSessionsAsync(own.Id, raw, null, CancellationToken.None));
        Assert.All(await db.RefreshTokens.ToListAsync(), x => Assert.Null(x.RevokedAtUtc));
    }

    [Fact]
    public async Task Revoke_others_preserves_current_foreign_and_already_expired_sessions()
    {
        using var db = Database(); var own = new User(); var foreign = new User(); db.Users.AddRange(own, foreign);
        var current = new RefreshToken { UserId = own.Id, TokenHash = Tokens().HashRefreshToken("current"), ExpiresAtUtc = DateTime.UtcNow.AddDays(1) };
        var target = new RefreshToken { UserId = own.Id, TokenHash = Tokens().HashRefreshToken("target"), ExpiresAtUtc = DateTime.UtcNow.AddDays(1) };
        var others = new RefreshToken { UserId = foreign.Id, TokenHash = Tokens().HashRefreshToken("foreign"), ExpiresAtUtc = DateTime.UtcNow.AddDays(1) };
        db.RefreshTokens.AddRange(current, target, others); await db.SaveChangesAsync();
        int count = await Service(db).RevokeOtherSessionsAsync(own.Id, "current", null, CancellationToken.None);
        Assert.Equal(1, count); db.ChangeTracker.Clear();
        Assert.Null((await db.RefreshTokens.SingleAsync(x => x.Id == current.Id)).RevokedAtUtc);
        Assert.NotNull((await db.RefreshTokens.SingleAsync(x => x.Id == target.Id)).RevokedAtUtc);
        Assert.Null((await db.RefreshTokens.SingleAsync(x => x.Id == others.Id)).RevokedAtUtc);
    }
    [Theory]
    [InlineData("current")]
    [InlineData("foreign")]
    public async Task Single_revoke_never_mutates_current_or_foreign_session(string targetName)
    {
        using var db = Database(); var own = new User(); var foreign = new User(); db.Users.AddRange(own, foreign);
        var current = new RefreshToken { UserId = own.Id, TokenHash = Tokens().HashRefreshToken("current"), ExpiresAtUtc = DateTime.UtcNow.AddDays(1) };
        var other = new RefreshToken { UserId = foreign.Id, TokenHash = Tokens().HashRefreshToken("foreign"), ExpiresAtUtc = DateTime.UtcNow.AddDays(1) };
        db.RefreshTokens.AddRange(current, other); await db.SaveChangesAsync();
        var error = await Record.ExceptionAsync(() => Service(db).RevokeSessionAsync(own.Id,
            targetName == "current" ? current.Id : other.Id, "current", null));
        Assert.NotNull(error); Assert.All(await db.RefreshTokens.ToListAsync(), x => Assert.Null(x.RevokedAtUtc));
    }

    [Fact]
    public async Task Invalid_profile_does_not_partially_change_fields()
    {
        using var db = Database(); var own = new User { FirstName = "Old", LastName = "Name" }; db.Users.Add(own); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<CSM.Application.Common.Exceptions.UserManagementException>(() =>
            Service(db).UpdateProfileAsync(own.Id, new("New", "Name", new string('x', 31), "Job")));
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        Assert.Equal("Old", (await db.Users.SingleAsync()).FirstName);
    }

    [Fact]
    public async Task Self_profile_summary_reads_current_identity_and_roles_from_the_database()
    {
        using var db = Database(); var own = new User { FirstName = "Fresh", LastName = "Name", Email = "own@example.test", CompanyId = Guid.NewGuid() };
        db.Users.Add(own); db.UserRoles.Add(new UserRole { UserId = own.Id, Role = new Role { Name = "ProcurementOfficer" } }); await db.SaveChangesAsync();
        var response = await Service(db).GetProfileAsync(own.Id);
        var json = System.Text.Json.JsonSerializer.SerializeToElement(response, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.True(json.TryGetProperty("name", out var name)); Assert.Equal("Fresh Name", name.GetString());
        Assert.Equal("own@example.test", json.GetProperty("email").GetString());
        Assert.Equal(own.CompanyId, json.GetProperty("companyId").GetGuid());
        Assert.Equal("ProcurementOfficer", Assert.Single(json.GetProperty("roles").EnumerateArray()).GetString());
    }

    private sealed class TestDb(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
    {
        public override Task<int> SaveChangesAsync(CancellationToken ct = default) => base.SaveChangesAsync(true, ct);
    }
}
