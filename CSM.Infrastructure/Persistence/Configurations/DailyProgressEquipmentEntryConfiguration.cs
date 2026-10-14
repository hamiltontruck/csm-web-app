using CSM.Domain.Entities.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CSM.Infrastructure.Persistence.Configurations;

public class DailyProgressEquipmentEntryConfiguration
    : IEntityTypeConfiguration<DailyProgressEquipmentEntry>
{
    public void Configure(EntityTypeBuilder<DailyProgressEquipmentEntry> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.EquipmentName)
               .HasMaxLength(200)
               .IsRequired();

        builder.Property(x => x.OperatorName)
               .HasMaxLength(150);

        builder.Property(x => x.HoursUsed)
               .HasPrecision(8,2);

        builder.HasOne(x => x.DailyProgressTaskEntry)
               .WithMany(x => x.EquipmentEntries)
               .HasForeignKey(x => x.DailyProgressTaskEntryId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}