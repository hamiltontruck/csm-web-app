using CSM.Application.Common.Exceptions;
using CSM.Application.Procurement.EquipmentAssignments.Dtos;
using CSM.Application.Procurement.EquipmentMaintenance.Dtos;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Entities.Procurement;
using CSM.Domain.Enums;
using CSM.Infrastructure.Services;
using Xunit;

namespace CSM.Infrastructure.Tests;

/// <summary>
/// Fixture-only lifecycle and company boundary checks for global SuperAdmin.
/// No production database, Render instance or external service is accessed.
/// </summary>
public sealed class SuperAdminAssignmentMaintenanceLifecycleTests
{
    private static readonly DateTime Started = new(2026, 11, 20, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Companyless_superadmin_assignment_create_edit_read_release_preserves_company()
    {
        using var f = new SuperAdminEquipmentCreateTests.Fixture("SuperAdmin", false);
        var equipment = NewEquipment(f.Beta.Id);
        var site = NewSite(f.Beta.Id);
        f.Db.AddRange(equipment, site);
        await f.Db.SaveChangesAsync();

        var service = new EquipmentAssignmentService(f.Db);
        var created = await service.CreateAsync(f.Actor.Id,
            new CreateEquipmentAssignmentRequest(equipment.Id, site.Id, null, Started, 10m, "initial"));

        var updated = await service.UpdateAsync(f.Actor.Id, created.Id,
            new UpdateEquipmentAssignmentRequest(Started, null, 12m, null, "edited"));
        var beforeRelease = await service.GetByIdAsync(f.Actor.Id, created.Id);

        Assert.Equal(f.Beta.Id, created.CompanyId);
        Assert.Equal(f.Beta.Id, updated.CompanyId);
        Assert.Equal("edited", beforeRelease.Remarks);
        Assert.Equal(12m, beforeRelease.MeterReadingAtAssignment);
        Assert.Equal(EquipmentStatus.Assigned, equipment.Status);

        var released = await service.ReleaseAsync(f.Actor.Id, created.Id, Started.AddHours(6), 15m);
        var saved = await service.GetByIdAsync(f.Actor.Id, created.Id);

        Assert.Equal(Started.AddHours(6), released.ReleasedAtUtc);
        Assert.Equal(15m, saved.MeterReadingAtRelease);
        Assert.Equal(f.Beta.Id, saved.CompanyId);
        Assert.Equal(EquipmentStatus.Available, equipment.Status);
        Assert.Equal(15m, equipment.CurrentMeterReading);
        Assert.Equal(f.Beta.Id, f.Db.EquipmentAssignments.Single().CompanyId);
        Assert.Null(f.Actor.CompanyId);
        await Assert.ThrowsAsync<EquipmentAssignmentManagementException>(
            () => service.ReleaseAsync(f.Actor.Id, created.Id, Started.AddHours(7), 16m));
        Assert.Single(f.Db.EquipmentAssignments);
    }

    [Fact]
    public async Task Ordinary_company_actor_cannot_read_update_or_release_foreign_assignment()
    {
        using var f = new SuperAdminEquipmentCreateTests.Fixture("SuperAdmin", false);
        var equipment = NewEquipment(f.Beta.Id);
        var site = NewSite(f.Beta.Id);
        f.Db.AddRange(equipment, site);
        await f.Db.SaveChangesAsync();

        var service = new EquipmentAssignmentService(f.Db);
        var created = await service.CreateAsync(f.Actor.Id,
            new CreateEquipmentAssignmentRequest(equipment.Id, site.Id, null, Started, 5m, null));

        f.Actor.CompanyId = f.Alpha.Id;
        f.Actor.UserRoles.Single().Role.Name = "CompanyAdmin";
        await f.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<EquipmentAssignmentManagementException>(
            () => service.GetByIdAsync(f.Actor.Id, created.Id));
        await Assert.ThrowsAsync<EquipmentAssignmentManagementException>(
            () => service.UpdateAsync(f.Actor.Id, created.Id,
                new UpdateEquipmentAssignmentRequest(Started, null, 99m, null, "forged")));
        await Assert.ThrowsAsync<EquipmentAssignmentManagementException>(
            () => service.ReleaseAsync(f.Actor.Id, created.Id, Started.AddHours(1), 9m));

        var persisted = f.Db.EquipmentAssignments.Single();
        Assert.Equal(f.Beta.Id, persisted.CompanyId);
        Assert.Null(persisted.ReleasedAtUtc);
        Assert.Equal(5m, persisted.MeterReadingAtAssignment);
        Assert.Equal(EquipmentStatus.Assigned, equipment.Status);
    }

    [Fact]
    public async Task Companyless_superadmin_maintenance_create_edit_read_preserves_equipment_company()
    {
        using var f = new SuperAdminEquipmentCreateTests.Fixture("SuperAdmin", false);
        var equipment = NewEquipment(f.Beta.Id);
        var site = NewSite(f.Beta.Id);
        f.Db.AddRange(equipment, site);
        await f.Db.SaveChangesAsync();

        var service = new EquipmentMaintenanceService(f.Db);
        var created = await service.CreateAsync(f.Actor.Id,
            new CreateEquipmentMaintenanceRequest(equipment.Id, "Inspection", Started,
                4m, 0m, "ETB", null, null, null, null, null, site.Id));

        var changed = await service.UpdateAsync(f.Actor.Id, created.Id,
            new UpdateEquipmentMaintenanceRequest("Repair", Started,
                Started.AddHours(1), Started.AddHours(3),
                7m, 200m, "ETB", "Local garage", "Completed", "Filter", null, null));
        var saved = await service.GetByIdAsync(f.Actor.Id, created.Id);

        Assert.Equal("Repair", changed.MaintenanceType);
        Assert.Equal("Repair", saved.MaintenanceType);
        Assert.Equal(200m, saved.Cost);
        Assert.Equal(f.Beta.Id, saved.CompanyId);
        Assert.Equal(site.Id, saved.SiteId);
        Assert.Equal(EquipmentStatus.Available, equipment.Status);
        Assert.Equal(7m, equipment.CurrentMeterReading);
        Assert.Equal(f.Beta.Id, f.Db.EquipmentMaintenanceRecords.Single().CompanyId);
        Assert.Null(f.Actor.CompanyId);

        var foreignSite = NewSite(f.Alpha.Id);
        f.Db.ConstructionSites.Add(foreignSite);
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<EquipmentMaintenanceManagementException>(() =>
            service.CreateAsync(f.Actor.Id,
                new CreateEquipmentMaintenanceRequest(equipment.Id, "Bad link", Started,
                    1m, 0m, "ETB", null, null, null, null, null, foreignSite.Id)));
        Assert.Single(f.Db.EquipmentMaintenanceRecords);
    }

    [Fact]
    public async Task Ordinary_company_actor_cannot_read_or_edit_foreign_maintenance()
    {
        using var f = new SuperAdminEquipmentCreateTests.Fixture("SuperAdmin", false);
        var equipment = NewEquipment(f.Beta.Id);
        f.Db.Equipment.Add(equipment);
        await f.Db.SaveChangesAsync();

        var service = new EquipmentMaintenanceService(f.Db);
        var created = await service.CreateAsync(f.Actor.Id,
            new CreateEquipmentMaintenanceRequest(equipment.Id, "Inspection", Started,
                3m, 0m, "ETB", null, null, null, null, null));

        f.Actor.CompanyId = f.Alpha.Id;
        f.Actor.UserRoles.Single().Role.Name = "CompanyAdmin";
        await f.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<EquipmentMaintenanceManagementException>(
            () => service.GetByIdAsync(f.Actor.Id, created.Id));
        await Assert.ThrowsAsync<EquipmentMaintenanceManagementException>(
            () => service.UpdateAsync(f.Actor.Id, created.Id,
                new UpdateEquipmentMaintenanceRequest("Forged", Started,
                    null, null, 10m, 500m, "ETB", null, null, null, null, null)));

        var saved = f.Db.EquipmentMaintenanceRecords.Single();
        Assert.Equal(f.Beta.Id, saved.CompanyId);
        Assert.Equal("Inspection", saved.MaintenanceType);
        Assert.Equal(3m, saved.MeterReading);
    }

    private static Equipment NewEquipment(Guid companyId) => new()
    {
        CompanyId = companyId,
        EquipmentCode = "REG-EQ",
        Name = "Test roller",
        IsActive = true,
        Status = EquipmentStatus.Available
    };

    private static ConstructionSite NewSite(Guid companyId) => new()
    {
        CompanyId = companyId,
        SiteCode = "REG-SITE",
        Name = "Fixture site",
        IsActive = true
    };
}
