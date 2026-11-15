using System.Security.Claims;
using CSM.Api.Controllers;
using CSM.Application.Procurement.EquipmentTimesheets;
using CSM.Domain.Entities.Equipment;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Equipment = CSM.Domain.Entities.Procurement.Equipment;
namespace CSM.Infrastructure.Tests;

public sealed class EquipmentTimesheetCompanyAccessTests
{
    [Fact]
    public async Task Companyless_superadmin_lists_and_opens_records_without_company_claim()
    {
        await using var f = await Fixture.CreateAsync();
        var controller = f.Controller(f.Admin);
        var rows = Assert.IsAssignableFrom<IReadOnlyList<EquipmentTimesheetResponse>>(
            Assert.IsType<OkObjectResult>((await controller.GetAll(null, default)).Result).Value);
        Assert.Equal(2, rows.Count);
        var one = Assert.IsType<EquipmentTimesheetResponse>(Assert.IsType<OkObjectResult>((await controller.GetById(f.OtherRow.Id, default)).Result).Value);
        Assert.Equal(f.OtherEquipment.Id, one.EquipmentId);
        Assert.Null((await f.Db.Users.SingleAsync(x => x.Id == f.Admin.Id)).CompanyId);
    }
    [Fact]
    public async Task Ordinary_actor_uses_database_company_even_when_claim_names_another_company()
    {
        await using var f = await Fixture.CreateAsync();
        var rows = Assert.IsAssignableFrom<IReadOnlyList<EquipmentTimesheetResponse>>(
            Assert.IsType<OkObjectResult>((await f.Controller(f.Ordinary, f.Other.Id).GetAll(null, default)).Result).Value);
        Assert.Equal(f.OwnEquipment.Id, Assert.Single(rows).EquipmentId);
        var missing = await f.Controller(f.Ordinary, f.Other.Id).GetById(f.OtherRow.Id, default);
        Assert.Equal(404, Assert.IsAssignableFrom<ObjectResult>(missing.Result).StatusCode);
    }
    [Fact]
    public async Task Global_creation_derives_equipment_company_and_preserves_worked_hours()
    {
        await using var f = await Fixture.CreateAsync();
        var response = await f.Controller(f.Admin).Create(f.Request(f.OtherEquipment.Id, f.OtherSite.Id), default);
        var created = Assert.IsType<EquipmentTimesheetResponse>(Assert.IsType<OkObjectResult>(response.Result).Value);
        await using var check = f.Pg.Context();
        var row = await check.EquipmentTimesheets.SingleAsync(x => x.Id == created.Id);
        Assert.Equal(f.Other.Id, row.CompanyId); Assert.Equal(7.5m, row.WorkedHours);
        Assert.Null((await check.Users.SingleAsync(x => x.Id == f.Admin.Id)).CompanyId);
    }
    [Fact]
    public async Task Global_update_cannot_move_existing_record_to_another_company()
    {
        await using var f = await Fixture.CreateAsync();
        var result = await f.Controller(f.Admin).Update(f.OwnRow.Id, f.Request(f.OtherEquipment.Id, f.OtherSite.Id), default);
        Assert.Equal(400, Assert.IsAssignableFrom<ObjectResult>(result.Result).StatusCode);
        await using var check = f.Pg.Context();
        var row = await check.EquipmentTimesheets.SingleAsync(x => x.Id == f.OwnRow.Id);
        Assert.Equal(f.Own.Id, row.CompanyId); Assert.Equal(f.OwnEquipment.Id, row.EquipmentId);
    }
    [Fact]
    public async Task Global_create_rejects_foreign_site_without_writing_a_record()
    {
        await using var f = await Fixture.CreateAsync();
        var result = await f.Controller(f.Admin).Create(f.Request(f.OtherEquipment.Id, f.OwnSite.Id), default);
        Assert.Equal(400, Assert.IsAssignableFrom<ObjectResult>(result.Result).StatusCode);
        Assert.Equal(2, await f.Db.EquipmentTimesheets.CountAsync());
    }
    [Fact]
    public async Task Ungranted_superadmin_membership_does_not_expand_an_ordinary_role()
    {
        await using var f = await Fixture.CreateAsync();
        var role = f.Admin.UserRoles.Single().Role;
        f.Db.RoleGranularPermissions.RemoveRange(role.GranularPermissions);
        f.Admin.CompanyId = f.Own.Id;
        f.Db.UserRoles.Add(new() { UserId = f.Admin.Id, RoleId = f.Ordinary.UserRoles.Single().RoleId });
        await f.Db.SaveChangesAsync();
        var rows = Assert.IsAssignableFrom<IReadOnlyList<EquipmentTimesheetResponse>>(
            Assert.IsType<OkObjectResult>((await f.Controller(f.Admin, f.Other.Id).GetAll(null, default)).Result).Value);
        Assert.Equal(f.OwnEquipment.Id, Assert.Single(rows).EquipmentId);
    }
    [Fact]
    public async Task Revoked_write_grant_returns_forbidden_without_creating()
    {
        await using var f = await Fixture.CreateAsync();
        f.Db.RoleGranularPermissions.Remove(f.Admin.UserRoles.Single().Role.GranularPermissions.Single(x => x.Permission == GranularPermission.Procurement_Inventory_Adjust));
        await f.Db.SaveChangesAsync();
        var result = await f.Controller(f.Admin).Create(f.Request(f.OtherEquipment.Id, f.OtherSite.Id), default);
        Assert.Equal(403, Assert.IsAssignableFrom<ObjectResult>(result.Result).StatusCode);
        Assert.Equal(2, await f.Db.EquipmentTimesheets.CountAsync());
    }
    [Fact]
    public async Task Global_update_preserves_company_and_returns_selected_equipment_details()
    {
        await using var f = await Fixture.CreateAsync();
        var replacement = new Equipment { CompanyId = f.Own.Id, EquipmentCode = "SECOND", Name = "Second equipment" };
        f.Db.Equipment.Add(replacement); await f.Db.SaveChangesAsync();
        var result = await f.Controller(f.Admin).Update(f.OwnRow.Id, f.Request(replacement.Id, f.OwnSite.Id), default);
        var row = Assert.IsType<EquipmentTimesheetResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(replacement.Id, row.EquipmentId); Assert.Equal("SECOND", row.EquipmentCode); Assert.Equal(7.5m, row.WorkedHours);
        await using var check = f.Pg.Context();
        var saved = await check.EquipmentTimesheets.SingleAsync(x => x.Id == f.OwnRow.Id);
        Assert.Equal(f.Own.Id, saved.CompanyId); Assert.Equal(replacement.Id, saved.EquipmentId);
    }
    [Fact]
    public async Task Write_grant_from_another_role_does_not_expand_global_read_authority_to_global_writes()
    {
        await using var f = await Fixture.CreateAsync();
        f.Db.RoleGranularPermissions.Remove(f.Admin.UserRoles.Single().Role.GranularPermissions.Single(x => x.Permission == GranularPermission.Procurement_Inventory_Adjust));
        f.Admin.CompanyId = f.Own.Id;
        f.Db.UserRoles.Add(new() { UserId = f.Admin.Id, RoleId = f.Ordinary.UserRoles.Single().RoleId });
        await f.Db.SaveChangesAsync();
        var result = await f.Controller(f.Admin).Create(f.Request(f.OtherEquipment.Id, f.OtherSite.Id), default);
        Assert.Equal(400, Assert.IsAssignableFrom<ObjectResult>(result.Result).StatusCode);
        Assert.Equal(2, await f.Db.EquipmentTimesheets.CountAsync());
    }
    private sealed class Fixture : IAsyncDisposable
    {
        public FinanceSettlementPostgresFixture Pg = null!; public ApplicationDbContext Db = null!;
        public Company Own = new() { Code = "OWN", Name = "Own", IsActive = true }, Other = new() { Code = "OTHER", Name = "Other", IsActive = true };
        public User Admin = null!, Ordinary = null!; public ConstructionSite OwnSite = null!, OtherSite = null!;
        public Equipment OwnEquipment = null!, OtherEquipment = null!; public EquipmentTimesheet OwnRow = null!, OtherRow = null!;
        public static async Task<Fixture> CreateAsync()
        {
            var f = new Fixture { Pg = await FinanceSettlementPostgresFixture.CreateAsync() }; f.Db = f.Pg.Context();
            var permissions = new[] { GranularPermission.Procurement_Inventory_View, GranularPermission.Procurement_Inventory_Adjust };
            f.Admin = new() { Email = "global@example.test", NormalizedEmail = "GLOBAL@EXAMPLE.TEST", IsActive = true,
                UserRoles = [new() { Role = FinanceOfficerAuthorityTests.Role("SuperAdmin", permissions) }] };
            f.Ordinary = new() { CompanyId = f.Own.Id, Email = "ordinary@example.test", NormalizedEmail = "ORDINARY@EXAMPLE.TEST", IsActive = true,
                UserRoles = [new() { Role = FinanceOfficerAuthorityTests.Role("CompanyAdmin", permissions) }] };
            f.OwnSite = new() { CompanyId = f.Own.Id, SiteCode = "OWN", Name = "Own site", IsActive = true };
            f.OtherSite = new() { CompanyId = f.Other.Id, SiteCode = "OTHER", Name = "Other site", IsActive = true };
            f.OwnEquipment = new() { CompanyId = f.Own.Id, EquipmentCode = "OWN", Name = "Own equipment" };
            f.OtherEquipment = new() { CompanyId = f.Other.Id, EquipmentCode = "OTHER", Name = "Other equipment" };
            f.OwnRow = Row(f.Own.Id, f.OwnEquipment.Id, f.OwnSite.Id); f.OtherRow = Row(f.Other.Id, f.OtherEquipment.Id, f.OtherSite.Id);
            f.Db.AddRange(f.Own, f.Other, f.Admin, f.Ordinary, f.OwnSite, f.OtherSite, f.OwnEquipment, f.OtherEquipment, f.OwnRow, f.OtherRow);
            await f.Db.SaveChangesAsync(); return f;
        }
        private static EquipmentTimesheet Row(Guid company, Guid equipment, Guid site) => new() { CompanyId = company, EquipmentId = equipment, SiteId = site,
            WorkDate = new(2026, 11, 1), StartTime = new(8, 0), EndTime = new(16, 0), WorkedHours = 8 };
        public CreateEquipmentTimesheetRequest Request(Guid equipment, Guid site) => new(equipment, site, new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), "Operator", "Day", TimeSpan.FromHours(8), TimeSpan.FromHours(16), 30, "Compaction", null);
        public EquipmentTimesheetsController Controller(User user, Guid? claimCompany = null)
        {
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, user.Id.ToString()), new(ClaimTypes.Role, "SuperAdmin") };
            if (claimCompany.HasValue) claims.Add(new("company_id", claimCompany.Value.ToString()));
            return new(new EquipmentTimesheetService(Db)) { ControllerContext = new() { HttpContext = new DefaultHttpContext { User = new(new ClaimsIdentity(claims, "test")) } } };
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await Pg.DisposeAsync(); }
    }
}
