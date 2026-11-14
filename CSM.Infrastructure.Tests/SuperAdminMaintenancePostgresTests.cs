using CSM.Application.Common.Exceptions;
using CSM.Application.Procurement.EquipmentMaintenance.Dtos;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Entities.Procurement;
using CSM.Domain.Enums;
using CSM.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class SuperAdminMaintenancePostgresTests
{
    [Theory]
    [InlineData(false, true)] [InlineData(true, true)] [InlineData(false, false)]
    public async Task Maintenance_uses_equipment_company_for_global_superadmin(bool actorHasOtherCompany, bool hasSite)
    {
        await using var f = await FinanceSettlementPostgresFixture.CreateAsync();
        await using var db = f.Context();
        var company = new Company { Code = "MAINT", Name = "Equipment company", IsActive = true };
        var other = new Company { Code = "OTHER", Name = "Other company", IsActive = true };
        var site = new ConstructionSite { CompanyId = company.Id, SiteCode = "MORMOR", Name = "MORMOR", IsActive = true };
        var equipment = new Equipment { CompanyId = company.Id, EquipmentCode = "EQ001", Name = "Roller", IsActive = true };
        var actor = new User { CompanyId = actorHasOtherCompany ? other.Id : null, Email = "maintenance@example.test", NormalizedEmail = "MAINTENANCE@EXAMPLE.TEST", IsActive = true,
            UserRoles = [new() { Role = FinanceOfficerAuthorityTests.Role("SuperAdmin", GranularPermission.Procurement_Inventory_View, GranularPermission.Procurement_Inventory_Adjust) }] };
        db.AddRange(company, other, site, equipment, actor); await db.SaveChangesAsync();
        var request = new CreateEquipmentMaintenanceRequest(equipment.Id, "service", new(2026,11,14), 3.5m, 0, "ETB", null, null, null, null, null, hasSite ? site.Id : null);
        var result = await new EquipmentMaintenanceService(db).CreateAsync(actor.Id, request);
        await using var verify = f.Context();
        var row = await verify.EquipmentMaintenanceRecords.SingleAsync();
        Assert.Equal(company.Id, row.CompanyId); Assert.Equal(equipment.Id, row.EquipmentId); Assert.Equal(hasSite ? site.Id : (Guid?)null, row.SiteId);
        Assert.Equal(3.5m, row.MeterReading); Assert.Equal(DateTimeKind.Utc, row.ScheduledAtUtc.Kind); Assert.Equal(actor.Id, row.CreatedBy); Assert.Equal(result.Id, row.Id);
        var read = await new EquipmentMaintenanceService(verify).GetByIdAsync(actor.Id, row.Id); Assert.Equal(row.Id, read.Id);
        // A global actor must still use a site in the equipment's company.
        var foreignSite = new ConstructionSite { CompanyId = other.Id, SiteCode = "OTHER", Name = "Other", IsActive = true };
        verify.Add(foreignSite); await verify.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<EquipmentMaintenanceManagementException>(() => new EquipmentMaintenanceService(verify).CreateAsync(actor.Id, request with { SiteId = foreignSite.Id }));
        Assert.Contains("equipment company", error.Message); Assert.Single(await verify.EquipmentMaintenanceRecords.ToListAsync());
    }
}
