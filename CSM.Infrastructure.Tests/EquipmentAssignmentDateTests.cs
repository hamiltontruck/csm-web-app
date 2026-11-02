using CSM.Domain.Constants;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Entities.Procurement;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class EquipmentAssignmentDateTests
{
    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    [InlineData(DateTimeKind.Utc)]
    public async Task Assignment_lifecycle_normalizes_dates_before_query_and_persistence(DateTimeKind kind)
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
        var service = new EquipmentAssignmentService(db);
        var created = await service.CreateAsync(actor.Id, new(equipment.Id, site.Id, null, date, 0, null));
        Assert.Equal(DateTimeKind.Utc, created.AssignedAtUtc.Kind);
        Assert.Equal(expected, created.AssignedAtUtc);
        var updated = await service.UpdateAsync(actor.Id, created.Id, new(date, date.AddHours(1), 0, 1, null));
        Assert.Equal(DateTimeKind.Utc, updated.AssignedAtUtc.Kind);
        Assert.Equal(DateTimeKind.Utc, updated.ReleasedAtUtc!.Value.Kind);
        Assert.Equal(expected.AddHours(1), updated.ReleasedAtUtc);
        await service.UpdateAsync(actor.Id, created.Id, new(date, null, 0, null, null));
        var released = await service.ReleaseAsync(actor.Id, created.Id, date.AddHours(2), 2);
        Assert.Equal(DateTimeKind.Utc, released.ReleasedAtUtc!.Value.Kind);
        Assert.Equal(expected.AddHours(2), released.ReleasedAtUtc);
    }

    private sealed class TestDb(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
    {
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            base.SaveChangesAsync(true, cancellationToken);
    }
}
