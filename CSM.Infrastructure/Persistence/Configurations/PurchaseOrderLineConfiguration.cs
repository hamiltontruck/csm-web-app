using CSM.Domain.Entities.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CSM.Infrastructure.Persistence.Configurations;

public class PurchaseOrderLineConfiguration :
    IEntityTypeConfiguration<PurchaseOrderLine>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderLine> builder)
    {
        builder.ToTable("PurchaseOrderLines");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.OrderedQuantity).HasPrecision(19, 4);
        builder.Property(x => x.ReceivedQuantity).HasPrecision(19, 4);
        builder.Property(x => x.UnitPrice).HasPrecision(19, 4);
        builder.Property(x => x.TaxAmount).HasPrecision(19, 4);
        builder.Property(x => x.LineTotal).HasPrecision(19, 4);

        builder.HasIndex(x => new
        {
            x.PurchaseOrderId,
            x.MaterialId
        });

        builder.HasOne(x => x.PurchaseOrder)
            .WithMany(x => x.Lines)
            .HasForeignKey(x => x.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Material)
            .WithMany(x => x.PurchaseOrderLines)
            .HasForeignKey(x => x.MaterialId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CostCode)
            .WithMany()
            .HasForeignKey(x => x.CostCodeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}