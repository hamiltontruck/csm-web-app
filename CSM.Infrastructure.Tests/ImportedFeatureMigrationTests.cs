using CSM.Domain.Entities.Identity;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class ImportedFeatureMigrationTests
{
    [Fact]
    public async Task PostgreSql_upgrade_and_rollback_preserve_existing_user_data()
    {
        var connection = new NpgsqlConnectionStringBuilder(
            Environment.GetEnvironmentVariable("CSM_TEST_POSTGRES") ??
            "Host=localhost;Port=5432;Username=postgres;Password=postgres");
        connection.Database = "csm_import_test_" + Guid.NewGuid().ToString("N");
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connection.ConnectionString).Options);
        const string baseline = "20261016161037_InitialPostgres";
        try
        {
            await db.Database.MigrateAsync(baseline);
            var id = Guid.NewGuid();
            db.Users.Add(new User { Id = id, Email = "existing@example.test", NormalizedEmail = "EXISTING@EXAMPLE.TEST",
                PasswordHash = "unchanged-existing-hash", FirstName = "Existing", LastName = "User", IsActive = true });
            await db.SaveChangesAsync();
            await db.Database.MigrateAsync();
            var user = await db.Users.AsNoTracking().SingleAsync(x => x.Id == id);
            Assert.Equal("unchanged-existing-hash", user.PasswordHash);
            var preferences = await new CSM.Infrastructure.Services.UserPreferencesService(db).GetAsync(id);
            Assert.Equal("en", preferences.Language);
            Assert.Single(await db.Set<UserPreferences>().Where(x => x.UserId == id).ToListAsync());
            db.Set<PrivacyDataRequest>().Add(new() { Id = Guid.NewGuid(), UserId = id, CompanyId = Guid.NewGuid(),
                RequestType = "DataCorrection", Description = "Migration persistence test" });
            await db.SaveChangesAsync();
            Assert.Single(await db.Set<PrivacyDataRequest>().Where(x => x.UserId == id).ToListAsync());
            await db.GetService<IMigrator>().MigrateAsync(baseline);
            db.ChangeTracker.Clear();
            Assert.Equal("unchanged-existing-hash", (await db.Users.SingleAsync(x => x.Id == id)).PasswordHash);
            Assert.Single(await db.Database.GetAppliedMigrationsAsync());
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }
}
