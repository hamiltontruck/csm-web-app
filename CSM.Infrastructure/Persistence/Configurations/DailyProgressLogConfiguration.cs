using CSM.Domain.Entities.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CSM.Infrastructure.Persistence.Configurations;

public class DailyProgressLogConfiguration :
    IEntityTypeConfiguration<DailyProgressLog>
{
    public void Configure(EntityTypeBuilder<DailyProgressLog> builder)
    {
        builder.ToTable("DailyProgressLogs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TemperatureCelsius)
            .HasPrecision(5, 2);

        builder.Property(x => x.ProgressPercentage)
            .HasPrecision(5, 2);

          builder.Property(x => x.LogNumber)
              .HasMaxLength(30)
              .IsRequired();

          builder.HasIndex(x => new
          {
              x.CompanyId,
              x.LogNumber
          }).IsUnique();

        builder.HasIndex(x => new
        {
            x.ProjectId,
            x.LogDate
        }).IsUnique();

        builder.HasOne(x => x.Project)
            .WithMany(x => x.DailyProgressLogs)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}