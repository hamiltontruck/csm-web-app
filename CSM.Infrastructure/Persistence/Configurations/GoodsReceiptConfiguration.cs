using CSM.Domain.Entities.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CSM.Infrastructure.Persistence.Configurations;

public class GoodsReceiptConfiguration :
    IEntityTypeConfiguration<GoodsReceipt>
{
    public void Configure(EntityTypeBuilder<GoodsReceipt> builder)
    {
        builder.ToTable("GoodsReceipts");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ReceiptNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.DeliveryNoteNumber).HasMaxLength(100);
        builder.Property(x => x.VehiclePlateNumber).HasMaxLength(50);

        builder.HasIndex(x => new
        {
            x.CompanyId,
            x.ReceiptNumber
        }).IsUnique();

        builder.HasOne(x => x.PurchaseOrder)
            .WithMany(x => x.GoodsReceipts)
            .HasForeignKey(x => x.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ConstructionSite)
            .WithMany(x => x.GoodsReceipts)
            .HasForeignKey(x => x.ConstructionSiteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}