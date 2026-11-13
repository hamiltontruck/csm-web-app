using CSM.Application.Auth.Dtos;
using CSM.Application.Common.Security;
using CSM.Domain.Entities.Identity;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Options;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class SettingsAccountPostgresTests
{
    [Fact]
    public async Task Simultaneous_first_loads_return_the_same_persisted_preferences()
    {
        var connection = new Npgsql.NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("CSM_TEST_POSTGRES")
            ?? "Host=localhost;Username=postgres;Password=postgres") { Database = "settings_race_" + Guid.NewGuid().ToString("N") };
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection.ConnectionString).Options;
        await using var setup = new ApplicationDbContext(options);
        try
        {
            await setup.Database.MigrateAsync();
            var actor = new User { Email = "own@example.test", NormalizedEmail = "OWN@EXAMPLE.TEST", PasswordHash = "test" };
            setup.Users.Add(actor); await setup.SaveChangesAsync();
            var barrier = new FirstLoadBarrier();
            await using var first = new ConcurrentDb(options, barrier); await using var second = new ConcurrentDb(options, barrier);
            var results = await Task.WhenAll(new CSM.Infrastructure.Services.UserPreferencesService(first).GetAsync(actor.Id),
                new CSM.Infrastructure.Services.UserPreferencesService(second).GetAsync(actor.Id));
            Assert.Equal(results[0], results[1]);
            Assert.Single(await setup.Set<UserPreferences>().AsNoTracking().Where(x => x.UserId == actor.Id).ToListAsync());
        }
        finally { await setup.Database.EnsureDeletedAsync(); }
    }

    private sealed class FirstLoadBarrier
    {
        private int _arrivals;
        private readonly TaskCompletionSource _both = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task Arrive()
        {
            if (Interlocked.Increment(ref _arrivals) == 2) _both.TrySetResult();
            await _both.Task.WaitAsync(TimeSpan.FromSeconds(15));
        }
    }
    private sealed class ConcurrentDb(DbContextOptions<ApplicationDbContext> options, FirstLoadBarrier barrier) : ApplicationDbContext(options)
    {
        public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            if (ChangeTracker.Entries<UserPreferences>().Any(x => x.State == EntityState.Added)) await barrier.Arrive();
            return await base.SaveChangesAsync(ct);
        }
    }

    [Fact]
    public async Task Additive_history_migration_preserves_accounts_and_records_new_attempts()
    {
        var connection = new Npgsql.NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("CSM_TEST_POSTGRES")
            ?? "Host=localhost;Username=postgres;Password=postgres") { Database = "settings_account_" + Guid.NewGuid().ToString("N") };
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection.ConnectionString).Options;
        await using var db = new ApplicationDbContext(options);
        try
        {
            var migrations = db.Database.GetMigrations().ToArray();
            var latest = Assert.Single(migrations.Where(x => x.EndsWith("_AddAccountLoginHistory")));
            var previous = migrations[Array.IndexOf(migrations, latest) - 1];
            await db.GetService<IMigrator>().MigrateAsync(previous);
            var hasher = new PasswordHasher();
            var actor = new User { FirstName = "Test", LastName = "User", Email = "own@example.test",
                NormalizedEmail = "OWN@EXAMPLE.TEST", PasswordHash = hasher.Hash("Test-only-Password123!") };
            db.Users.Add(actor); await db.SaveChangesAsync();
            await db.GetService<IMigrator>().MigrateAsync(latest);
            var jwt = Options.Create(new JwtSettings { Issuer = "test", Audience = "test", SecretKey = "test-only-signing-key-with-at-least-32-characters" });
            var service = new AuthService(db, hasher, new TokenService(jwt), jwt);
            await service.LoginAsync(new LoginRequest(actor.Email, "Test-only-Password123!", "test browser"), "127.0.0.1");
            await Assert.ThrowsAsync<CSM.Application.Common.Exceptions.AuthenticationException>(() =>
                service.LoginAsync(new LoginRequest(actor.Email, "wrong", "test browser"), "127.0.0.1"));
            await using (var fresh = new ApplicationDbContext(options))
            {
                var history = await fresh.Set<LoginHistoryEntry>().Where(x => x.UserId == actor.Id).ToListAsync();
                Assert.Equal(2, history.Count); Assert.Single(history.Where(x => x.Succeeded));
                Assert.Single(history.Where(x => !x.Succeeded));
            }
            await db.GetService<IMigrator>().MigrateAsync(previous);
            db.ChangeTracker.Clear(); Assert.True(await db.Users.AnyAsync(x => x.Id == actor.Id));
            await db.GetService<IMigrator>().MigrateAsync(latest);
            Assert.Empty(await db.Set<LoginHistoryEntry>().ToListAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
