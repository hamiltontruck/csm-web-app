using CSM.Domain.Entities.HRM;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CSM.Infrastructure.Persistence.Configurations;

public class AttendanceConfiguration :
    IEntityTypeConfiguration<Attendance>
{
    public void Configure(EntityTypeBuilder<Attendance> builder)
    {
        builder.ToTable("Attendances");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(x => x.Source)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(x => x.CheckInLatitude).HasPrecision(10, 7);
        builder.Property(x => x.CheckInLongitude).HasPrecision(10, 7);
        builder.Property(x => x.CheckOutLatitude).HasPrecision(10, 7);
        builder.Property(x => x.CheckOutLongitude).HasPrecision(10, 7);

        builder.Property(x => x.CheckInAccuracyMeters)
            .HasPrecision(10, 2);

        builder.Property(x => x.CheckOutAccuracyMeters)
            .HasPrecision(10, 2);

        builder.Property(x => x.RegularHours)
            .HasPrecision(8, 2);

        builder.Property(x => x.OvertimeHours)
            .HasPrecision(8, 2);

        builder.HasIndex(x => new
        {
            x.EmployeeId,
            x.ConstructionSiteId,
            x.AttendanceDate
        }).IsUnique();

        builder.HasOne(x => x.Employee)
            .WithMany(x => x.Attendances)
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ConstructionSite)
            .WithMany(x => x.Attendances)
            .HasForeignKey(x => x.ConstructionSiteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Shift)
            .WithMany()
            .HasForeignKey(x => x.ShiftId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}