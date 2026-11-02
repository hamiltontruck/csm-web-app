using CSM.Domain.Entities.Organization;
using CSM.Domain.Entities.Procurement;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class EquipmentHourLogDateTests
{
    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    [InlineData(DateTimeKind.Utc)]
    public async Task Create_normalizes_log_date_and_saves_meter_difference(DateTimeKind kind)
    {
        using var db = new TestDb(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var company = Guid.NewGuid();
        var site = new ConstructionSite { Id = Guid.NewGuid(), CompanyId = company, Name = "Hamilton Test Site" };
        var equipment = new Equipment { Id = Guid.NewGuid(), CompanyId = company, Name = "Test Mixer", EquipmentCode = "EQ-TEST" };
        db.AddRange(site, equipment);
        db.SaveChanges(true);
        var date = new DateTime(2026, 11, 2, 0, 0, 0, kind);
        var expected = kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(date, DateTimeKind.Utc) : date.ToUniversalTime();
        var response = await new EquipmentHourLogService(db).CreateAsync(company,
            new(equipment.Id, site.Id, date, 0, 2, "Test Operator", "Concrete mixing test", "hour meter test"));
        Assert.Equal(DateTimeKind.Utc, response.LoggedAtUtc.Kind);
        Assert.Equal(expected, response.LoggedAtUtc);
        Assert.Equal(2m, response.HoursWorked);
        db.ChangeTracker.Clear();
        var saved = Assert.Single(db.EquipmentHourLogs);
        Assert.Equal(DateTimeKind.Utc, saved.LoggedAtUtc.Kind);
        Assert.Equal(2m, Assert.Single(db.Equipment).CurrentMeterReading);
    }

    private sealed class TestDb(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
    {
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            base.SaveChangesAsync(true, cancellationToken);
    }
}
