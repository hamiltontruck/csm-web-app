using CSM.Domain.Entities.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CSM.Infrastructure.Persistence.Configurations;

public class DailyMaterialUsageConfiguration :
    IEntityTypeConfiguration<DailyMaterialUsage>
{
    public void Configure(
        EntityTypeBuilder<DailyMaterialUsage> builder)
    {
        builder.ToTable("DailyMaterialUsages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.QuantityIssued)
            .HasPrecision(19, 4);

        builder.Property(x => x.QuantityUsed)
            .HasPrecision(19, 4);

        builder.Property(x => x.QuantityReturned)
            .HasPrecision(19, 4);

        builder.Property(x => x.QuantityVariance)
            .HasPrecision(19, 4);

        builder.Property(x => x.Unit)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.Notes)
            .HasMaxLength(1000);

        builder.Property(x => x.UsageNumber)
            .HasMaxLength(30)
            .IsRequired();

        builder.HasIndex(x => new
        {
            x.CompanyId,
            x.UsageNumber
        }).IsUnique();

        builder.HasIndex(x => new
        {
            x.ConstructionSiteId,
            x.Date
        });

        builder.HasIndex(x => new
        {
            x.MaterialId,
            x.Date
        });

        builder.HasIndex(x => new
        {
            x.DailyProgressLogId,
            x.MaterialId
        });

        builder.HasOne(x => x.ConstructionSite)
            .WithMany()
            .HasForeignKey(x => x.ConstructionSiteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Material)
            .WithMany()
            .HasForeignKey(x => x.MaterialId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.DailyProgressLog)
            .WithMany()
            .HasForeignKey(x => x.DailyProgressLogId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.LoggedByUser)
            .WithMany()
            .HasForeignKey(x => x.LoggedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}


