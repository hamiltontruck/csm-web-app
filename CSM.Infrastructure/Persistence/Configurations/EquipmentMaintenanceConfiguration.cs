using CSM.Domain.Entities.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CSM.Infrastructure.Persistence.Configurations;

public class EquipmentMaintenanceConfiguration :
    IEntityTypeConfiguration<EquipmentMaintenance>
{
    public void Configure(EntityTypeBuilder<EquipmentMaintenance> builder)
    {
        builder.ToTable("EquipmentMaintenance");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.MaintenanceType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.MeterReading).HasPrecision(19, 4);
        builder.Property(x => x.Cost).HasPrecision(19, 4);
        builder.Property(x => x.NextMaintenanceMeterReading).HasPrecision(19, 4);

        builder.Property(x => x.CurrencyCode)
            .HasMaxLength(3);

        builder.HasIndex(x => new
        {
            x.EquipmentId,
            x.ScheduledAtUtc
        });

        builder.HasOne(x => x.Equipment)
            .WithMany(x => x.MaintenanceRecords)
            .HasForeignKey(x => x.EquipmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}