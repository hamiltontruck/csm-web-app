using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace CSM.Infrastructure.Tests;

internal sealed class FinanceSettlementPostgresFixture : IAsyncDisposable
{
    internal DbContextOptions<ApplicationDbContext> Options { get; }
    private FinanceSettlementPostgresFixture(string connection) => Options = new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseNpgsql(connection, p => p.EnableRetryOnFailure()).Options;
    internal ApplicationDbContext Context() => new(Options);
    internal static async Task<FinanceSettlementPostgresFixture> CreateAsync(string? targetMigration = null)
    {
        var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("CSM_TEST_POSTGRES") ?? "Host=localhost;Port=5432;Username=postgres;Password=postgres")
        { Database = "csm_settlement_" + Guid.NewGuid().ToString("N") };
        var fixture = new FinanceSettlementPostgresFixture(connection.ConnectionString);
        await using var db = fixture.Context();
        try { await db.GetService<IMigrator>().MigrateAsync(targetMigration); }
        catch { await fixture.DisposeAsync(); throw; }
        return fixture;
    }
    public async ValueTask DisposeAsync()
    {
        await using var db = Context(); await db.Database.EnsureDeletedAsync();
    }
}
