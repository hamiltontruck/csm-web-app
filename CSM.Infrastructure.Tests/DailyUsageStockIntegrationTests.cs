using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using CSM.Application.Common.Exceptions;
using CSM.Application.Inventory.DailyMaterialUsage.Dtos;
using CSM.Application.Inventory.StockIssues.Dtos;
using CSM.Application.Notifications;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Entities.Procurement;
using CSM.Domain.Entities.Projects;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace CSM.Infrastructure.Tests;

// Uses a disposable database on the local PostgreSQL test server provisioned by CI.
public sealed class DailyUsageStockIntegrationTests
{
    [Theory]
    [InlineData(2, 2, 0)]
    [InlineData(2, 1, 1)]
    [InlineData(2, 0, 0)]
    [InlineData(10, 10, 0)]
    public async Task Usage_records_reconciliation_without_moving_previously_issued_stock(int issued, int used, int returned)
    {
        await using var fixture = await Fixture.CreateAsync();
        var db = fixture.Db;
        await new StockIssueService(db, new Notifications()).CreateAsync(fixture.ActorId, new CreateStockIssueRequest
        {
            ConstructionSiteId = fixture.SiteId, MaterialId = fixture.MaterialId,
            IssueDateUtc = DateTime.UtcNow, ReferenceNumber = "ISS-001", Quantity = issued
        });
        db.ChangeTracker.Clear();
        Assert.Equal(10m - issued, (await db.StockItems.SingleAsync()).QuantityOnHand);
        var issueMovement = await db.StockMovements.SingleAsync();
        var result = await new DailyMaterialUsageService(db).CreateAsync(fixture.ActorId,
            fixture.Request(issued, used, returned));
        db.ChangeTracker.Clear();

        Assert.Equal(10m - issued, (await db.StockItems.SingleAsync()).QuantityOnHand);
        Assert.Equal(issueMovement.Id, (await db.StockMovements.SingleAsync()).Id);
        var saved = await db.DailyMaterialUsages.SingleAsync();
        Assert.Equal(issued, saved.QuantityIssued);
        Assert.Equal(used, saved.QuantityUsed);
        Assert.Equal(returned, saved.QuantityReturned);
        Assert.Equal(issued - used - returned, saved.QuantityVariance);
        Assert.Equal(issued != used + returned, saved.RequiresSupervisorReview);
        Assert.Equal(saved.Id, result.Id);
    }

    [Fact]
    public async Task Invalid_reconciliation_does_not_write_usage_or_move_stock()
    {
        await using var fixture = await Fixture.CreateAsync();
        await Assert.ThrowsAsync<DailyMaterialUsageManagementException>(() =>
            new DailyMaterialUsageService(fixture.Db).CreateAsync(fixture.ActorId, fixture.Request(2, 3, 0)));
        Assert.Empty(await fixture.Db.DailyMaterialUsages.ToListAsync());
        Assert.Empty(await fixture.Db.StockMovements.ToListAsync());
        Assert.Equal(10m, (await fixture.Db.StockItems.SingleAsync()).QuantityOnHand);
    }

    [Fact]
    public async Task Wrong_company_cannot_write_usage()
    {
        await using var fixture = await Fixture.CreateAsync();
        var actor = await fixture.Db.Users.Include(x => x.UserRoles).ThenInclude(x => x.Role).SingleAsync();
        actor.CompanyId = Guid.NewGuid();
        // Do not save a forged company FK; the loaded actor is used by the service.
        actor.UserRoles.Single().Role.Name = AppRoles.CompanyAdmin;
        await Assert.ThrowsAsync<DailyMaterialUsageManagementException>(() =>
            new DailyMaterialUsageService(fixture.Db).CreateAsync(fixture.ActorId, fixture.Request(2, 2, 0)));
        Assert.Empty(await fixture.Db.DailyMaterialUsages.ToListAsync());
    }

    // The runtime model still carries one SQL Server filtered-index expression.
    // Translate that DDL only while creating this disposable PostgreSQL schema.
    private sealed class IndexSyntax : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            command.CommandText = command.CommandText.Replace("[IsDeleted] = 0", "\"IsDeleted\" = FALSE");
            return ValueTask.FromResult(result);
        }
    }

    private sealed class Notifications : ILowStockNotificationService
    {
        public Task NotifyIfLowStockAsync(Guid stockItemId, Guid actorUserId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public ApplicationDbContext Db { get; }
        public Guid ActorId { get; } = Guid.NewGuid();
        public Guid SiteId { get; } = Guid.NewGuid();
        public Guid MaterialId { get; } = Guid.NewGuid();
        private Guid LogId { get; } = Guid.NewGuid();
        private DateOnly Date { get; } = DateOnly.FromDateTime(DateTime.UtcNow);

        private Fixture(ApplicationDbContext db) => Db = db;

        public CreateDailyMaterialUsageRequest Request(int issued, int used, int returned) => new()
        {
            ConstructionSiteId = SiteId, MaterialId = MaterialId, DailyProgressLogId = LogId,
            Date = Date, QuantityIssued = issued, QuantityUsed = used, QuantityReturned = returned, Unit = "Bag"
        };

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new NpgsqlConnectionStringBuilder(
                Environment.GetEnvironmentVariable("CSM_TEST_POSTGRES") ??
                "Host=localhost;Port=5432;Username=postgres;Password=postgres");
            connection.Database = "csm_usage_test_" + Guid.NewGuid().ToString("N");
            var fixture = new Fixture(new ApplicationDbContext(
                new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection.ConnectionString).AddInterceptors(new IndexSyntax()).Options));
            try
            {
                await fixture.Db.Database.EnsureCreatedAsync();
                var company = new Company { Id = Guid.NewGuid(), Code = "TEST", Name = "Test Company" };
                var site = new ConstructionSite { Id = fixture.SiteId, CompanyId = company.Id, Company = company,
                    SiteCode = "SITE", Name = "Test Site" };
                var material = new Material { Id = fixture.MaterialId, CompanyId = company.Id,
                    MaterialCode = "MAT-001", Name = "Cement", Category = "Cement", UnitOfMeasure = "Bag" };
                fixture.Db.Users.Add(new User { Id = fixture.ActorId, Email = "test@example.com",
                    FirstName = "Test", LastName = "Admin", IsActive = true,
                    UserRoles = [new UserRole { Id = Guid.NewGuid(), Role = new Role {
                        Id = Guid.NewGuid(), Name = AppRoles.SuperAdmin } }] });
                fixture.Db.StockItems.Add(new StockItem { Id = Guid.NewGuid(), CompanyId = company.Id,
                    ConstructionSite = site, ConstructionSiteId = site.Id, Material = material, MaterialId = material.Id,
                    QuantityOnHand = 10m, AverageUnitCost = 1000m });
                fixture.Db.DailyProgressLogs.Add(new DailyProgressLog { Id = fixture.LogId, CompanyId = company.Id,
                    LogNumber = "DPL-001", LogDate = fixture.Date, Project = new Project { Id = Guid.NewGuid(),
                        CompanyId = company.Id, ConstructionSite = site, ConstructionSiteId = site.Id,
                        ProjectCode = "PROJECT", Name = "Test Project" } });
                await fixture.Db.SaveChangesAsync();
                return fixture;
            }
            catch
            {
                await fixture.DisposeAsync();
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await Db.Database.EnsureDeletedAsync();
            await Db.DisposeAsync();
        }
    }
}
