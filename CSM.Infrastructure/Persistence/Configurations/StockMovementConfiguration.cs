using CSM.Domain.Entities.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CSM.Infrastructure.Persistence.Configurations;

public class StockMovementConfiguration :
    IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("StockMovements");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.MovementType)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(x => x.Quantity)
            .HasPrecision(19, 4);

        builder.Property(x => x.StockBalanceBefore)
            .HasPrecision(19, 4);

        builder.Property(x => x.StockBalanceAfter)
            .HasPrecision(19, 4);

        builder.Property(x => x.UnitCost)
            .HasPrecision(19, 4);

        builder.Property(x => x.ReferenceType).HasMaxLength(50);
        builder.Property(x => x.ReferenceNumber).HasMaxLength(100);

        builder.HasIndex(x => new
        {
            x.StockItemId,
            x.MovementDateUtc
        });

        builder.HasOne(x => x.StockItem)
            .WithMany(x => x.Movements)
            .HasForeignKey(x => x.StockItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
