using CSM.Domain.Entities.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CSM.Infrastructure.Persistence.Configurations;

public class DailyProgressTaskEntryConfiguration
    : IEntityTypeConfiguration<DailyProgressTaskEntry>
{
    public void Configure(EntityTypeBuilder<DailyProgressTaskEntry> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ProgressPercentage)
            .HasPrecision(5, 2);

        builder.Property(x => x.EquipmentHours)
            .HasPrecision(8, 2);

        builder.Property(x => x.WorkCompleted)
            .HasMaxLength(2000);

        builder.Property(x => x.Remarks)
            .HasMaxLength(1000);

        // Daily Progress Log → Task Entries (1:N)
        builder.HasOne(x => x.DailyProgressLog)
            .WithMany(x => x.DailyProgressTaskEntries)
            .HasForeignKey(x => x.DailyProgressLogId)
            .OnDelete(DeleteBehavior.Cascade);

        // Work Task → Task Entries (1:N)
        builder.HasOne(x => x.WorkTask)
            .WithMany(x => x.DailyProgressEntries)
            .HasForeignKey(x => x.WorkTaskId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}