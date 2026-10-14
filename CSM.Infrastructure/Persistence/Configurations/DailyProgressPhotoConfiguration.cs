using CSM.Domain.Entities.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CSM.Infrastructure.Persistence.Configurations;

public sealed class DailyProgressPhotoConfiguration
    : IEntityTypeConfiguration<DailyProgressPhoto>
{
    public void Configure(EntityTypeBuilder<DailyProgressPhoto> builder)
    {
        builder.ToTable("DailyProgressPhotos");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.FileName)
            .HasMaxLength(260)
            .IsRequired();

        builder.Property(x => x.FilePath)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.Caption)
            .HasMaxLength(500);

        builder.HasOne(x => x.DailySiteDiary)
            .WithMany(x => x.ProgressPhotos)
            .HasForeignKey(x => x.DailySiteDiaryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}