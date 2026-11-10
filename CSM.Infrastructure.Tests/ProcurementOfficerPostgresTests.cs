using System.Reflection;
using CSM.Application.Common.Exceptions;
using CSM.Application.Inventory.StockAdjustments.Dtos;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Notifications;
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
            var firstStock = await db.StockItems.SingleAsync(x => x.ConstructionSiteId == first.Id);
            var secondStock = await db.StockItems.SingleAsync(x => x.ConstructionSiteId == second.Id);
            var outgoing = new StockMovement { CompanyId = company.Id, StockItemId = firstStock.Id, MovementType = StockMovementType.TransferOut,
                ReferenceType = "StockTransfer", ReferenceNumber = "PAIR", Quantity = 1m, MovementDateUtc = DateTime.UtcNow };
            var incoming = new StockMovement { CompanyId = company.Id, StockItemId = secondStock.Id, MovementType = StockMovementType.TransferIn,
                ReferenceType = "StockTransfer", ReferenceId = outgoing.Id, ReferenceNumber = "PAIR", Quantity = 1m, MovementDateUtc = DateTime.UtcNow };
            db.StockMovements.AddRange(outgoing, incoming);
            db.Notifications.Add(new Notification { CompanyId = company.Id, RecipientUserId = actor.Id, Title = "Transfer", Message = "Transfer details",
                SourceEntityType = "StockMovement", SourceEntityId = outgoing.Id });
            await db.SaveChangesAsync();
            Assert.Single(await new StockMovementService(db).GetAllAsync(actor.Id)); // Only the permitted adjustment.
            await Assert.ThrowsAsync<StockMovementManagementException>(() => new StockMovementService(db).GetByIdAsync(actor.Id, outgoing.Id));
            Assert.Empty(await new NotificationService(db).GetAllAsync(actor.Id, null));
            Assert.Equal(0, await new NotificationService(db).GetUnreadCountAsync(actor.Id));
            assignment.IsActive = false; await db.SaveChangesAsync();
            Assert.Equal(2, (await stock.GetAllAsync(actor.Id)).Count);
            Assert.Equal(3, (await new StockMovementService(db).GetAllAsync(actor.Id)).Count);
            Assert.Single(await new NotificationService(db).GetAllAsync(actor.Id, null));
            Assert.Equal(2, (await new ReportingService(db).GetExecutiveDashboardAsync(actor.Id)).TotalSites);
            // A successful DTO alone is insufficient: re-query persisted read state.
            var notifications = new NotificationService(db);
            var visible = Assert.Single(await notifications.GetAllAsync(actor.Id, null));
            db.ChangeTracker.Clear();
            Assert.True((await notifications.MarkReadAsync(actor.Id, visible.Id, new(true))).IsRead);
            db.ChangeTracker.Clear();
            var saved = await db.Notifications.AsNoTracking().SingleAsync(x => x.Id == visible.Id);
            Assert.True(saved.IsRead);
            Assert.NotNull(saved.ReadAtUtc);
            Assert.Equal(0, await notifications.GetUnreadCountAsync(actor.Id));
            Assert.False((await notifications.MarkReadAsync(actor.Id, visible.Id, new(false))).IsRead);
            db.ChangeTracker.Clear();
            saved = await db.Notifications.AsNoTracking().SingleAsync(x => x.Id == visible.Id);
            Assert.False(saved.IsRead);
            Assert.Null(saved.ReadAtUtc);
            Assert.Equal(1, await notifications.GetUnreadCountAsync(actor.Id));
            var activeAssignment = await db.UserSiteAccesses.SingleAsync(x => x.Id == assignment.Id);
            activeAssignment.IsActive = true;
            var request = new MaterialRequest { CompanyId = company.Id, ConstructionSiteId = first.Id,
                RequestNumber = "MARK-READ", RequestDate = new(2026, 11, 10), RequestedBy = actor.Id };
            var requestNotice = new Notification { CompanyId = company.Id, RecipientUserId = actor.Id,
                Title = "Material Request Created", Message = "Permitted material request", SourceEntityType = "MaterialRequest",
                SourceEntityId = request.Id };
            db.MaterialRequests.Add(request); db.Notifications.Add(requestNotice);
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            Assert.Equal(requestNotice.Id, Assert.Single(await notifications.GetAllAsync(actor.Id, null)).Id);
            Assert.Equal(1, await notifications.GetUnreadCountAsync(actor.Id));
            Assert.True((await notifications.MarkReadAsync(actor.Id, requestNotice.Id, new(true))).IsRead);
            db.ChangeTracker.Clear();
            Assert.True((await db.Notifications.AsNoTracking().SingleAsync(x => x.Id == requestNotice.Id)).IsRead);
            Assert.True(Assert.Single(await notifications.GetAllAsync(actor.Id, null)).IsRead);
            Assert.Equal(0, await notifications.GetUnreadCountAsync(actor.Id));
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

