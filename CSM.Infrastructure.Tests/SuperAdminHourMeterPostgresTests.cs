using System.Security.Claims;
using CSM.Api.Controllers;
using CSM.Application.Procurement.EquipmentHourLogs;
using CSM.Domain.Entities.Equipment;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Entities.Procurement;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace CSM.Infrastructure.Tests;

public sealed class SuperAdminHourMeterPostgresTests
{
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Global_superadmin_reads_and_creates_without_company_claim(bool assignedToOtherCompany)
    {
        await using var f = await Fixture.CreateAsync("SuperAdmin", assignedToOtherCompany);
        var controller = f.Controller();
        var all = Assert.IsAssignableFrom<IReadOnlyList<EquipmentHourLogResponse>>(Assert.IsType<OkObjectResult>((await controller.GetAll(null, default)).Result).Value);
        Assert.Equal(2, all.Count);
        var filtered = Assert.IsAssignableFrom<IReadOnlyList<EquipmentHourLogResponse>>(Assert.IsType<OkObjectResult>((await controller.GetAll(f.Equipment.Id, default)).Result).Value);
        Assert.Single(filtered); Assert.Equal(f.Equipment.Id, filtered[0].EquipmentId);
        var result = await controller.Create(new(f.Equipment.Id, f.Site.Id, new(2026,11,14), 2, 3.5m, "Operator", "Rolling", "Hour test"), default);
        var response = Assert.IsType<EquipmentHourLogResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        await using var check = f.Pg.Context(); var row = await check.EquipmentHourLogs.SingleAsync(x => x.Id == response.Id);
        Assert.Equal(f.Company.Id, row.CompanyId); Assert.Equal(f.Site.Id, row.ProjectId); Assert.Equal(1.5m, row.HoursWorked); Assert.Equal(DateTimeKind.Utc, row.LoggedAtUtc.Kind);
        Assert.Equal(3.5m, (await check.Equipment.SingleAsync(x => x.Id == f.Equipment.Id)).CurrentMeterReading);
    }

    [Fact]
    public async Task Company_user_ignores_forged_company_claim_and_cannot_write_foreign_equipment()
    {
        await using var f = await Fixture.CreateAsync("CompanyAdmin", false);
        f.Actor.CompanyId = f.Company.Id; await f.Db.SaveChangesAsync();
        var controller = f.Controller(f.Other.Id);
        var rows = Assert.IsAssignableFrom<IReadOnlyList<EquipmentHourLogResponse>>(Assert.IsType<OkObjectResult>((await controller.GetAll(null, default)).Result).Value);
        Assert.Single(rows); Assert.Equal(f.Equipment.Id, rows[0].EquipmentId);
        var result = await controller.Create(new(f.ForeignEquipment.Id, f.ForeignSite.Id, DateTime.UtcNow, 2, 4, "Operator", "Invalid", null), default);
        Assert.Equal(403, Assert.IsType<ObjectResult>(result.Result).StatusCode);
        await using var check = f.Pg.Context(); Assert.Equal(2, await check.EquipmentHourLogs.CountAsync()); Assert.Equal(2, (await check.Equipment.SingleAsync(x => x.Id == f.ForeignEquipment.Id)).CurrentMeterReading);
    }

    [Theory] [InlineData("view-revoked")] [InlineData("adjust-revoked")] [InlineData("inactive")] [InlineData("officer")]
    public async Task Live_access_denial_does_not_change_records_or_equipment(string reason)
    {
        await using var f = await Fixture.CreateAsync(reason == "officer" ? "ProcurementOfficer" : "SuperAdmin", false);
        if (reason.EndsWith("revoked")) f.Db.RoleGranularPermissions.Remove(f.Role.GranularPermissions.Single(x => x.Permission == (reason == "view-revoked" ? GranularPermission.Procurement_Inventory_View : GranularPermission.Procurement_Inventory_Adjust)));
        if (reason == "inactive") f.Actor.IsActive = false;
        await f.Db.SaveChangesAsync();
        var controller = f.Controller();
        if (reason != "adjust-revoked") Assert.Equal(403, Assert.IsType<ObjectResult>((await controller.GetAll(null, default)).Result).StatusCode);
        var result = await controller.Create(new(f.Equipment.Id, f.Site.Id, DateTime.UtcNow, 2, 5, "Operator", "Invalid", null), default);
        Assert.Equal(403, Assert.IsType<ObjectResult>(result.Result).StatusCode);
        await using var check = f.Pg.Context(); Assert.Equal(2, await check.EquipmentHourLogs.CountAsync()); Assert.Equal(2, (await check.Equipment.SingleAsync(x => x.Id == f.Equipment.Id)).CurrentMeterReading);
    }

    [Fact]
    public async Task Global_write_requires_site_in_equipment_company()
    {
        await using var f = await Fixture.CreateAsync("SuperAdmin", false);
        var result = await f.Controller().Create(new(f.Equipment.Id, f.ForeignSite.Id, DateTime.UtcNow, 2, 5, "Operator", "Invalid", null), default);
        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(2, await f.Db.EquipmentHourLogs.CountAsync()); Assert.Equal(2, f.Equipment.CurrentMeterReading);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public FinanceSettlementPostgresFixture Pg = null!;
        public ApplicationDbContext Db = null!;
        public Company Company = new() { Code = "HOUR", Name = "Equipment company", IsActive = true }, Other = new() { Code = "OTHER", Name = "Other company", IsActive = true };
        public ConstructionSite Site = null!, ForeignSite = null!;
        public Equipment Equipment = null!, ForeignEquipment = null!;
        public User Actor = null!; public Role Role = null!;
        public static async Task<Fixture> CreateAsync(string role, bool otherCompany)
        {
            var f = new Fixture { Pg = await FinanceSettlementPostgresFixture.CreateAsync() }; f.Db = f.Pg.Context();
            f.Site = new() { CompanyId = f.Company.Id, Name = "MORMOR", SiteCode = "MORMOR", IsActive = true };
            f.ForeignSite = new() { CompanyId = f.Other.Id, Name = "Other", SiteCode = "OTHER", IsActive = true };
            f.Equipment = new() { CompanyId = f.Company.Id, EquipmentCode = "EQ001", Name = "Roller", CurrentMeterReading = 2, IsActive = true };
            f.ForeignEquipment = new() { CompanyId = f.Other.Id, EquipmentCode = "FOREIGN", Name = "Other", CurrentMeterReading = 2, IsActive = true };
            f.Role = FinanceOfficerAuthorityTests.Role(role, GranularPermission.Procurement_Inventory_View, GranularPermission.Procurement_Inventory_Adjust);
            f.Actor = new() { CompanyId = otherCompany ? f.Other.Id : null, Email = "hour@example.test", NormalizedEmail = "HOUR@EXAMPLE.TEST", IsActive = true, UserRoles = [new() { Role = f.Role }] };
            f.Db.AddRange(f.Company, f.Other, f.Site, f.ForeignSite, f.Equipment, f.ForeignEquipment, f.Actor,
                new EquipmentHourLog { CompanyId = f.Company.Id, EquipmentId = f.Equipment.Id, ProjectId = f.Site.Id, LoggedAtUtc = DateTime.UtcNow, CurrentMeterReading = 2, HoursWorked = 2 },
                new EquipmentHourLog { CompanyId = f.Other.Id, EquipmentId = f.ForeignEquipment.Id, ProjectId = f.ForeignSite.Id, LoggedAtUtc = DateTime.UtcNow, CurrentMeterReading = 2, HoursWorked = 2 });
            await f.Db.SaveChangesAsync(); return f;
        }
        public EquipmentHourLogsController Controller(Guid? forgedCompany = null)
        {
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, Actor.Id.ToString()), new(ClaimTypes.Role, Role.Name) };
            if (forgedCompany.HasValue) claims.Add(new("company_id", forgedCompany.ToString()!));
            return new(new EquipmentHourLogService(Db)) { ControllerContext = new() { HttpContext = new DefaultHttpContext { User = new(new ClaimsIdentity(claims, "test")) } } };
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await Pg.DisposeAsync(); }
    }
}
