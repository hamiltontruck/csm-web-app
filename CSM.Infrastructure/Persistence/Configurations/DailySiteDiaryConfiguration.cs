using CSM.Domain.Entities.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CSM.Infrastructure.Persistence.Configurations;

public sealed class DailySiteDiaryConfiguration
    : IEntityTypeConfiguration<DailySiteDiary>
{
    public void Configure(EntityTypeBuilder<DailySiteDiary> builder)
    {
        builder.ToTable("DailySiteDiaries");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Weather)
            .IsRequired();

        builder.Property(x => x.WorkPerformed)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(x => x.DelayReason)
            .HasMaxLength(1000);

        builder.Property(x => x.SafetyObservation)
            .HasMaxLength(1000);

        builder.HasOne(x => x.Site)
            .WithMany()
            .HasForeignKey(x => x.SiteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Project)
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => new
        {
            x.CompanyId,
            x.SiteId,
            x.DiaryDate
        }).IsUnique();

        builder.Property(x => x.Temperature)
            .HasPrecision(5, 2);
    }
}