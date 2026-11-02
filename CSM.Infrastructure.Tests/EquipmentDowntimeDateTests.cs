using CSM.Domain.Constants;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Entities.Procurement;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class EquipmentDowntimeDateTests
{
    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    [InlineData(DateTimeKind.Utc)]
    public async Task Downtime_lifecycle_normalizes_dates_before_validation_and_persistence(DateTimeKind kind)
    {
        using var db = new TestDb(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var company = Guid.NewGuid();
        var actor = new User { Id = Guid.NewGuid(), CompanyId = company, IsActive = true,
            UserRoles = [new UserRole { Id = Guid.NewGuid(), Role = new Role { Id = Guid.NewGuid(), Name = AppRoles.SiteManager } }] };
        var site = new ConstructionSite { Id = Guid.NewGuid(), CompanyId = company, Name = "Test site" };
        var equipment = new Equipment { Id = Guid.NewGuid(), CompanyId = company, EquipmentCode = "EQ-TEST", Name = "Mixer" };
        db.AddRange(actor, site, equipment);
        db.SaveChanges(true);
        var date = new DateTime(2026, 11, 2, 12, 0, 0, kind);
        var expected = kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(date, DateTimeKind.Utc) : date.ToUniversalTime();
        var service = new EquipmentDowntimeService(db);
        var created = await service.CreateAsync(actor.Id, new(equipment.Id, site.Id, date, null, "DT-TEST", "Test reason", null));
        Assert.Equal(DateTimeKind.Utc, created.StartedAtUtc.Kind);
        Assert.Equal(expected, created.StartedAtUtc);
        Assert.Null(created.EndedAtUtc);
        var updated = await service.UpdateAsync(actor.Id, created.Id, new(site.Id, date, date.AddHours(1), "Test reason", "Fixed"));
        Assert.Equal(DateTimeKind.Utc, updated.StartedAtUtc.Kind);
        Assert.Equal(DateTimeKind.Utc, updated.EndedAtUtc!.Value.Kind);
        Assert.Equal(expected.AddHours(1), updated.EndedAtUtc);
        await service.UpdateAsync(actor.Id, created.Id, new(site.Id, date, null, "Test reason", null));
        var closed = await service.CloseAsync(actor.Id, created.Id, date.AddHours(2), "Fixed");
        Assert.Equal(DateTimeKind.Utc, closed.EndedAtUtc!.Value.Kind);
        Assert.Equal(expected.AddHours(2), closed.EndedAtUtc);

    }

    private sealed class TestDb(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
    {
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            base.SaveChangesAsync(true, cancellationToken);
    }
}
