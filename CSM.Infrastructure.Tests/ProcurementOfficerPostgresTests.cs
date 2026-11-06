using System.Reflection;
using CSM.Application.Common.Exceptions;
using CSM.Application.Inventory.StockAdjustments.Dtos;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Entities.Procurement;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class ProcurementOfficerPostgresTests
{
    [Fact]
    public async Task PostgreSQL_translates_conditional_scope_and_denies_outside_site_before_stock_write()
    {
        var connection = new Npgsql.NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("CSM_TEST_POSTGRES") ?? "Host=localhost;Username=postgres;Password=postgres") {
            Database = "procurement_scope_" + Guid.NewGuid().ToString("N") };
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection.ConnectionString).Options);
        try
        {
            await db.Database.MigrateAsync();
            var company = new Company { Code = "OWN", Name = "Own" };
            var otherCompany = new Company { Code = "OTHER", Name = "Other" };
            var first = new ConstructionSite { CompanyId = company.Id, SiteCode = "FIRST", Name = "First", IsActive = true };
            var second = new ConstructionSite { CompanyId = company.Id, SiteCode = "SECOND", Name = "Second", IsActive = true };
            var foreign = new ConstructionSite { CompanyId = otherCompany.Id, SiteCode = "FOREIGN", Name = "Foreign", IsActive = true };
            var material = new Material { CompanyId = company.Id, MaterialCode = "MAT", Name = "Material", Category = "General", UnitOfMeasure = "Bag", IsActive = true };
            var actor = new User { CompanyId = company.Id, Email = "officer@example.test", NormalizedEmail = "OFFICER@EXAMPLE.TEST", IsActive = true,
                UserRoles = [new() { Role = new Role { Name = "ProcurementOfficer", NormalizedName = "PROCUREMENTOFFICER",
                    GranularPermissions = Enum.GetValues<GranularPermission>().Select(p => new RoleGranularPermission { Permission = p }).ToList() } }] };
            db.Companies.AddRange(company, otherCompany); db.ConstructionSites.AddRange(first, second, foreign); db.Materials.Add(material); db.Users.Add(actor);
            db.StockItems.AddRange(new StockItem { CompanyId = company.Id, ConstructionSiteId = first.Id, MaterialId = material.Id },
                new StockItem { CompanyId = company.Id, ConstructionSiteId = second.Id, MaterialId = material.Id });
            await db.SaveChangesAsync();
            var sites = new SiteService(db);
            var stock = new StockBalanceService(db);
            Assert.Equal(2, (await sites.GetAllAsync(actor.Id)).Count);
            Assert.Equal(2, (await stock.GetAllAsync(actor.Id)).Count);
            var assignment = new UserSiteAccess { UserId = actor.Id, CompanyId = company.Id, SiteId = first.Id, IsActive = true };
            db.UserSiteAccesses.Add(assignment); await db.SaveChangesAsync();
            Assert.Equal(first.Id, Assert.Single(await sites.GetAllAsync(actor.Id)).Id);
            Assert.Equal(first.Id, Assert.Single(await stock.GetAllAsync(actor.Id)).ConstructionSiteId);
            Assert.Empty(await new PurchaseOrderService(db).GetAllAsync(actor.Id, null, null, null));
            Assert.Empty(await new MaterialRequestService(db).GetAllAsync(actor.Id));
            var change = new CreateStockAdjustmentRequest { ConstructionSiteId = second.Id, MaterialId = material.Id, Increase = true,
                Quantity = 1m, UnitCost = 1m, AdjustmentNumber = "ADJ", Reason = "Verification", AdjustmentDateUtc = DateTime.UtcNow };
            await Assert.ThrowsAsync<StockAdjustmentManagementException>(() => new StockAdjustmentService(db).CreateAsync(actor.Id, change));
            Assert.Empty(await db.StockMovements.AsNoTracking().ToListAsync());
            Assert.Equal(0m, await db.StockItems.AsNoTracking().Where(x => x.ConstructionSiteId == second.Id).Select(x => x.QuantityOnHand).SingleAsync());
            change.ConstructionSiteId = first.Id;
            await new StockAdjustmentService(db).CreateAsync(actor.Id, change);
            Assert.Equal(1m, Assert.Single(await stock.GetAllAsync(actor.Id)).QuantityOnHand);
            Assert.Single(await new StockMovementService(db).GetAllAsync(actor.Id));
            assignment.IsActive = false; await db.SaveChangesAsync();
            Assert.Equal(2, (await stock.GetAllAsync(actor.Id)).Count);
            Assert.Equal(2, (await new ReportingService(db).GetExecutiveDashboardAsync(actor.Id)).TotalSites);
            // Relational uniqueness must hold for newly appended default permissions.
            var admin = new Role { Name = "SuperAdmin", NormalizedName = "SUPERADMIN" };
            var companyAdmin = new Role { Name = "CompanyAdmin", NormalizedName = "COMPANYADMIN" };
            db.Roles.AddRange(admin, companyAdmin); await db.SaveChangesAsync();
            var seed = typeof(DatabaseInitializer).GetMethod("SeedGranularRolePermissionsAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
            await (Task)seed.Invoke(null, new object[] { db, CancellationToken.None })!;
            await (Task)seed.Invoke(null, new object[] { db, CancellationToken.None })!;
            Assert.Single(await db.RoleGranularPermissions.Where(x => x.RoleId == admin.Id && x.Permission == GranularPermission.Procurement_Vendor_View).ToListAsync());
            Assert.Single(await db.RoleGranularPermissions.Where(x => x.RoleId == companyAdmin.Id && x.Permission == GranularPermission.Procurement_Vendor_View).ToListAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
