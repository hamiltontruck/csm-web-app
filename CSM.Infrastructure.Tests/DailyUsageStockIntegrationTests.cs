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

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    [InlineData(DateTimeKind.Utc)]
    public async Task Stock_return_accepts_browser_date_and_updates_stock_once(DateTimeKind kind)
    {
        await using var fixture = await Fixture.CreateAsync();
        var db = fixture.Db;
        await new StockIssueService(db, new Notifications()).CreateAsync(fixture.ActorId, new CreateStockIssueRequest
        {
            ConstructionSiteId = fixture.SiteId, MaterialId = fixture.MaterialId,
            IssueDateUtc = DateTime.UtcNow, ReferenceNumber = "ISS-002", Quantity = 2m
        });
        db.ChangeTracker.Clear();
        var issue = await db.StockMovements.SingleAsync();
        var date = new DateTime(2026, 10, 21, 0, 0, 0, kind);
        var request = new CSM.Application.Inventory.StockReturns.Dtos.CreateStockReturnRequest
        {
            OriginalIssueMovementId = issue.Id, Quantity = 1m, ReturnDateUtc = date,
            ReferenceNumber = "RET-001", ReturnedBy = "Test Construction Team", Reason = "Unused material"
        };

        var service = new StockReturnService(db);
        var result = await service.CreateAsync(fixture.ActorId, request);
        db.ChangeTracker.Clear();
        var saved = await db.StockMovements.SingleAsync(x => x.MovementType == CSM.Domain.Enums.StockMovementType.Return);
        Assert.Equal(DateTimeKind.Utc, saved.MovementDateUtc.Kind);
        Assert.Equal(kind == DateTimeKind.Local ? date.ToUniversalTime() : DateTime.SpecifyKind(date, DateTimeKind.Utc),
            saved.MovementDateUtc);
        Assert.Equal(DateTimeKind.Utc, result.ReturnDateUtc.Kind);
        Assert.Equal(saved.MovementDateUtc, result.ReturnDateUtc);
        Assert.Equal(8m, saved.StockBalanceBefore);
        Assert.Equal(9m, saved.StockBalanceAfter);
        Assert.Equal(9m, (await db.StockItems.SingleAsync()).QuantityOnHand);
        Assert.Equal(issue.Id, saved.ReferenceId);

        await Assert.ThrowsAsync<StockReturnManagementException>(() => service.CreateAsync(fixture.ActorId, request));
        db.ChangeTracker.Clear();
        Assert.Equal(9m, (await db.StockItems.SingleAsync()).QuantityOnHand);
        Assert.Equal(2, await db.StockMovements.CountAsync());
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

    [Fact]
    public async Task User_creation_persists_company_site_role_and_hashed_password_in_PostgreSQL()
    {
        await using var fixture = await Fixture.CreateAsync();
        var db = fixture.Db;
        var companyId = (await db.Companies.SingleAsync()).Id;
        db.Roles.Add(new Role { Name = AppRoles.Storekeeper, NormalizedName = "STOREKEEPER" });
        await db.SaveChangesAsync();
        var hasher = new CSM.Infrastructure.Security.PasswordHasher();
        var request = new CSM.Application.Users.Dtos.CreateUserRequest(
            companyId, null, "storekeeper@example.test", "Test", "Storekeeper", "Example-Password123!",
            "Test phone", "Storekeeper", fixture.SiteId, [AppRoles.Storekeeper], [fixture.SiteId]);
        var result = await new UserService(db, hasher).CreateAsync(fixture.ActorId, request);
        db.ChangeTracker.Clear();

        var saved = await db.Users.Include(x => x.SiteAccesses).Include(x => x.UserRoles)
            .ThenInclude(x => x.Role).SingleAsync(x => x.Id == result.Id);
        Assert.Equal(companyId, saved.CompanyId);
        Assert.Equal(fixture.SiteId, saved.DefaultSiteId);
        Assert.Equal("Storekeeper", saved.JobTitle);
        Assert.True(hasher.Verify(request.TemporaryPassword, saved.PasswordHash));
        Assert.True(saved.MustChangePassword);
        Assert.Equal(AppRoles.Storekeeper, Assert.Single(saved.UserRoles).Role.Name);
        Assert.Equal(fixture.SiteId, Assert.Single(saved.SiteAccesses).SiteId);
        Assert.True(Assert.Single(saved.SiteAccesses).IsDefault);
        Assert.Single(await db.UserAudits.Where(x => x.TargetUserId == result.Id).ToListAsync());
        Assert.Equal(10m, (await db.StockItems.SingleAsync()).QuantityOnHand);
    }

    [Theory]
    [InlineData("foreign")]
    [InlineData("missing")]
    [InlineData("inactive")]
    [InlineData("default-not-assigned")]
    public async Task Editing_sites_rejects_invalid_assignments_without_changing_existing_access(string reason)
    {
        await using var fixture = await Fixture.CreateAsync();
        var db = fixture.Db;
        var target = await CreateManagedUserAsync(fixture);
        var companyId = target.CompanyId!.Value;
        var otherCompany = new Company { Code = "OTHER", Name = "Other company" };
        var other = new ConstructionSite { Company = otherCompany, SiteCode = "OTHER", Name = "Foreign site" };
        var inactive = new ConstructionSite { CompanyId = companyId, SiteCode = "INACTIVE", Name = "Inactive site", IsActive = false };
        var unassigned = new ConstructionSite { CompanyId = companyId, SiteCode = "UNASSIGNED", Name = "Unassigned site" };
        db.ConstructionSites.AddRange(other, inactive, unassigned);
        await db.SaveChangesAsync();
        var requestedSite = reason switch { "foreign" => other.Id, "missing" => Guid.NewGuid(),
            "inactive" => inactive.Id, _ => fixture.SiteId };
        var requestedDefault = reason == "default-not-assigned" ? unassigned.Id : requestedSite;
        var auditsBefore = await db.UserAudits.CountAsync();
        await Assert.ThrowsAsync<UserManagementException>(() => new UserService(db,
            new CSM.Infrastructure.Security.PasswordHasher()).SetSitesAsync(fixture.ActorId, target.Id,
                new CSM.Application.Users.Dtos.SetUserSitesRequest(requestedDefault, [requestedSite])));
        db.ChangeTracker.Clear();
        var saved = await db.Users.Include(x => x.SiteAccesses).SingleAsync(x => x.Id == target.Id);
        Assert.Equal(fixture.SiteId, saved.DefaultSiteId);
        Assert.Equal(fixture.SiteId, Assert.Single(saved.SiteAccesses).SiteId);
        Assert.Equal(auditsBefore, await db.UserAudits.CountAsync());
    }

    [Fact]
    public async Task Editing_sites_adds_destination_preserves_role_and_reuses_removed_assignments()
    {
        await using var fixture = await Fixture.CreateAsync();
        var db = fixture.Db;
        var target = await CreateManagedUserAsync(fixture);
        var originalAccessId = (await db.UserSiteAccesses.SingleAsync()).Id;
        var destination = new ConstructionSite { CompanyId = target.CompanyId!.Value, SiteCode = "DEST", Name = "Destination" };
        db.ConstructionSites.Add(destination);
        await db.SaveChangesAsync();
        var service = new UserService(db, new CSM.Infrastructure.Security.PasswordHasher());
        var request = new CSM.Application.Users.Dtos.SetUserSitesRequest(fixture.SiteId, [fixture.SiteId, destination.Id]);
        await service.SetSitesAsync(fixture.ActorId, target.Id, request);
        db.ChangeTracker.Clear();
        var accesses = await db.UserSiteAccesses.Where(x => x.UserId == target.Id).ToListAsync();
        Assert.Equal(2, accesses.Count);
        Assert.Equal(originalAccessId, accesses.Single(x => x.SiteId == fixture.SiteId).Id);
        Assert.True(accesses.Single(x => x.SiteId == fixture.SiteId).IsDefault);
        Assert.False(accesses.Single(x => x.SiteId == destination.Id).IsDefault);
        await service.SetSitesAsync(fixture.ActorId, target.Id,
            new CSM.Application.Users.Dtos.SetUserSitesRequest(destination.Id, [destination.Id]));
        db.ChangeTracker.Clear();
        await service.SetSitesAsync(fixture.ActorId, target.Id, request);
        db.ChangeTracker.Clear();
        Assert.Equal(2, await db.UserSiteAccesses.CountAsync(x => x.UserId == target.Id));
        Assert.Equal(originalAccessId, (await db.UserSiteAccesses.SingleAsync(x => x.UserId == target.Id && x.SiteId == fixture.SiteId)).Id);
        var saved = await db.Users.Include(x => x.UserRoles).ThenInclude(x => x.Role).SingleAsync(x => x.Id == target.Id);
        Assert.Equal(AppRoles.Storekeeper, Assert.Single(saved.UserRoles).Role.Name);
        Assert.Equal(fixture.SiteId, saved.DefaultSiteId);
    }

    [Theory]
    [InlineData("StoreKeeper")]
    [InlineData("")]
    public async Task Editing_roles_handles_case_and_rejects_empty_selection(string requested)
    {
        await using var fixture = await Fixture.CreateAsync();
        var db = fixture.Db;
        var target = await CreateManagedUserAsync(fixture);
        var service = new UserService(db, new CSM.Infrastructure.Security.PasswordHasher());
        var request = new CSM.Application.Users.Dtos.SetUserRolesRequest(
            string.IsNullOrEmpty(requested) ? [] : [requested]);
        if (string.IsNullOrEmpty(requested))
            await Assert.ThrowsAsync<UserManagementException>(() => service.SetRolesAsync(
                fixture.ActorId, target.Id, request, default));
        else
            await service.SetRolesAsync(fixture.ActorId, target.Id, request, default);
        db.ChangeTracker.Clear();
        Assert.Equal("Storekeeper", (await db.Users.Include(x => x.UserRoles).ThenInclude(x => x.Role)
            .SingleAsync(x => x.Id == target.Id)).UserRoles.Single().Role.Name);
    }

    [Theory]
    [InlineData("Storekeeper")]
    [InlineData("CompanyAdmin")]
    public async Task Editing_roles_cannot_be_used_to_escalate_to_SuperAdmin(string actorRole)
    {
        await using var fixture = await Fixture.CreateAsync();
        var db = fixture.Db;
        var target = await CreateManagedUserAsync(fixture);
        Guid actorId = target.Id;
        if (actorRole == "CompanyAdmin")
        {
            var actor = new User { CompanyId = target.CompanyId, Email = "companyadmin@example.test",
                FirstName = "Company", LastName = "Admin", IsActive = true,
                UserRoles = [new UserRole { Role = new Role { Name = AppRoles.CompanyAdmin, NormalizedName = "COMPANYADMIN" } }] };
            db.Users.Add(actor);
            await db.SaveChangesAsync();
            actorId = actor.Id;
        }
        await Assert.ThrowsAsync<UserManagementException>(() => new UserService(db,
            new CSM.Infrastructure.Security.PasswordHasher()).SetRolesAsync(actorId, target.Id,
                new CSM.Application.Users.Dtos.SetUserRolesRequest([AppRoles.SuperAdmin]), default));
        db.ChangeTracker.Clear();
        Assert.Equal("Storekeeper", (await db.Users.Include(x => x.UserRoles).ThenInclude(x => x.Role)
            .SingleAsync(x => x.Id == target.Id)).UserRoles.Single().Role.Name);
    }

    private static async Task<CSM.Application.Users.Dtos.UserResponse> CreateManagedUserAsync(Fixture fixture)
    {
        var db = fixture.Db;
        db.Roles.Add(new Role { Name = AppRoles.Storekeeper, NormalizedName = "STOREKEEPER" });
        await db.SaveChangesAsync();
        var companyId = (await db.Companies.SingleAsync()).Id;
        return await new UserService(db, new CSM.Infrastructure.Security.PasswordHasher()).CreateAsync(fixture.ActorId,
            new CSM.Application.Users.Dtos.CreateUserRequest(companyId, null, "managed@example.test",
                "Test", "Storekeeper", "Example-Password123!", "", "Storekeeper", fixture.SiteId,
                [AppRoles.Storekeeper], [fixture.SiteId]));
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
