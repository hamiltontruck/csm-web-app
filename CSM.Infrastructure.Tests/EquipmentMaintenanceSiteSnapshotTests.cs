using System.Security.Claims;
using System.Text.Json;
using CSM.Api.Controllers;
using CSM.Application.Common.Exceptions;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Entities.Procurement;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;
using Request = CSM.Application.Procurement.EquipmentMaintenance.Dtos.CreateEquipmentMaintenanceRequest;

namespace CSM.Infrastructure.Tests;

public sealed class EquipmentMaintenanceSiteSnapshotTests
{
    [Fact]
    public async Task Assigned_manager_creation_persists_and_returns_the_requested_site()
    {
        using var f = new Fixture();
        var result = await new EquipmentMaintenanceService(f.Db).CreateAsync(f.Actor.Id, f.Request(f.Site.Id));
        Assert.Equal(f.Site.Id, SiteOf(result));
        Assert.Equal(f.Site.Id, SiteOf(Assert.Single(f.Db.EquipmentMaintenanceRecords)));
    }

    [Fact]
    public async Task Company_admin_can_capture_an_own_company_site_without_a_user_assignment()
    {
        using var f = new Fixture(AppRoles.CompanyAdmin);
        f.Db.UserSiteAccesses.RemoveRange(f.Db.UserSiteAccesses);
        f.Db.SaveChanges(true);
        var result = await new EquipmentMaintenanceService(f.Db).CreateAsync(f.Actor.Id, f.Request(f.OtherSite.Id));
        Assert.Equal(f.OtherSite.Id, SiteOf(result));
    }

    [Theory]
    [InlineData("foreign")]
    [InlineData("unassigned")]
    [InlineData("inactive")]
    [InlineData("missing")]
    public async Task Invalid_site_snapshot_is_rejected_before_record_creation(string kind)
    {
        using var f = new Fixture();
        var siteId = kind switch { "foreign" => f.ForeignSite.Id, "unassigned" => f.OtherSite.Id,
            "inactive" => f.InactiveSite.Id, _ => Guid.NewGuid() };
        await Assert.ThrowsAsync<EquipmentMaintenanceManagementException>(() =>
            new EquipmentMaintenanceService(f.Db).CreateAsync(f.Actor.Id, f.Request(siteId)));
        Assert.Empty(f.Db.EquipmentMaintenanceRecords);
    }

    [Fact]
    public async Task Legacy_company_maintenance_without_a_site_remains_unattributed()
    {
        using var f = new Fixture();
        var result = await new EquipmentMaintenanceService(f.Db).CreateAsync(f.Actor.Id, f.Request(null));
        Assert.Null(SiteOf(result));
        Assert.Null(SiteOf(Assert.Single(f.Db.EquipmentMaintenanceRecords)));
    }

    [Fact]
    public async Task Unassigned_site_rejection_is_HTTP_403()
    {
        using var f = new Fixture();
        var controller = new EquipmentMaintenanceController(new EquipmentMaintenanceService(f.Db)) {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext {
                User = new ClaimsPrincipal(new ClaimsIdentity([
                    new Claim(ClaimTypes.NameIdentifier, f.Actor.Id.ToString())], "test")) } } };
        var result = await controller.Create(f.Request(f.OtherSite.Id), default);
        Assert.Equal(403, Assert.IsType<ObjectResult>(result.Result).StatusCode);
        Assert.Empty(f.Db.EquipmentMaintenanceRecords);
    }

    [Fact]
    public void Migration_adds_nullable_site_without_guessing_legacy_attribution()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=migration_script_only;Username=postgres;Password=postgres").Options);
        var sql = db.GetService<IMigrator>().GenerateScript("20261031213000_AddUserAuditCompanySnapshot",
            "20261102212000_AddEquipmentMaintenanceSiteSnapshot");
        Assert.Contains("ADD \"SiteId\" uuid", sql);
        Assert.DoesNotContain("\"SiteId\" uuid NOT NULL", sql);
        Assert.DoesNotContain("UPDATE \"EquipmentMaintenance\"", sql);
    }

    private static Guid? SiteOf(object value) =>
        value.GetType().GetProperty("SiteId")?.GetValue(value) is Guid id ? id : null;

    private sealed class Fixture : IDisposable
    {
        public TestDb Db { get; } = new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        public User Actor { get; }
        public ConstructionSite Site { get; }
        public ConstructionSite OtherSite { get; }
        public ConstructionSite InactiveSite { get; }
        public ConstructionSite ForeignSite { get; }
        public Equipment Equipment { get; }

        public Fixture(string role = AppRoles.SiteManager)
        {
            var company = Guid.NewGuid();
            Actor = new User { Id = Guid.NewGuid(), CompanyId = company, IsActive = true,
                UserRoles = [new UserRole { Id = Guid.NewGuid(), Role = new Role { Id = Guid.NewGuid(), Name = role } }] };
            Site = new ConstructionSite { Id = Guid.NewGuid(), CompanyId = company, Name = "Assigned site", IsActive = true };
            OtherSite = new ConstructionSite { Id = Guid.NewGuid(), CompanyId = company, Name = "Other site", IsActive = true };
            InactiveSite = new ConstructionSite { Id = Guid.NewGuid(), CompanyId = company, Name = "Inactive site", IsActive = false };
            ForeignSite = new ConstructionSite { Id = Guid.NewGuid(), CompanyId = Guid.NewGuid(), Name = "Foreign site", IsActive = true };
            Equipment = new Equipment { Id = Guid.NewGuid(), CompanyId = company, EquipmentCode = "EQ-TEST", Name = "Test mixer", IsActive = true };
            Db.AddRange(Actor, Site, OtherSite, InactiveSite, ForeignSite, Equipment);
            Db.UserSiteAccesses.Add(new UserSiteAccess { Id = Guid.NewGuid(), UserId = Actor.Id,
                CompanyId = company, SiteId = Site.Id, IsActive = true });
            Db.SaveChanges(true);
        }

        public Request Request(Guid? site) => JsonSerializer.Deserialize<Request>(JsonSerializer.Serialize(new {
            EquipmentId = Equipment.Id, MaintenanceType = "Routine inspection", ScheduledAtUtc = new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc),
            MeterReading = (decimal?)2, Cost = (decimal?)0, CurrencyCode = "ETB", ServiceProvider = "Test provider",
            Description = "Site history test", PartsReplaced = (string?)null,
            NextMaintenanceAtUtc = (DateTime?)null, NextMaintenanceMeterReading = (decimal?)null, SiteId = site }))!;

        public void Dispose() => Db.Dispose();
    }

    private sealed class TestDb(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
    {
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            base.SaveChangesAsync(true, cancellationToken);
    }
}
