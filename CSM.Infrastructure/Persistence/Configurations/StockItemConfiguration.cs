using CSM.Domain.Entities.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CSM.Infrastructure.Persistence.Configurations;

public class StockItemConfiguration : IEntityTypeConfiguration<StockItem>
{
    public void Configure(EntityTypeBuilder<StockItem> builder)
    {
        builder.ToTable("StockItems");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.QuantityOnHand).HasPrecision(19, 4);
        builder.Property(x => x.QuantityReserved).HasPrecision(19, 4);
        builder.Property(x => x.ReorderLevel).HasPrecision(19, 4);
        builder.Property(x => x.MaximumStockLevel).HasPrecision(19, 4);
        builder.Property(x => x.AverageUnitCost).HasPrecision(19, 4);

        builder.Property(x => x.StorageLocation).HasMaxLength(100);

        builder.HasIndex(x => new
        {
            x.ConstructionSiteId,
            x.MaterialId
        }).IsUnique();

        builder.HasOne(x => x.ConstructionSite)
            .WithMany(x => x.StockItems)
            .HasForeignKey(x => x.ConstructionSiteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Material)
            .WithMany(x => x.StockItems)
            .HasForeignKey(x => x.MaterialId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}