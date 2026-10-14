using CSM.Domain.Entities.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CSM.Infrastructure.Persistence.Configurations;

public class PurchaseOrderConfiguration :
    IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.ToTable("PurchaseOrders");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.PurchaseOrderNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.CurrencyCode)
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(x => x.ExchangeRate).HasPrecision(19, 8);
        builder.Property(x => x.Subtotal).HasPrecision(19, 4);
        builder.Property(x => x.TaxAmount).HasPrecision(19, 4);
        builder.Property(x => x.DiscountAmount).HasPrecision(19, 4);
        builder.Property(x => x.TotalAmount).HasPrecision(19, 4);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.HasIndex(x => new
        {
            x.CompanyId,
            x.PurchaseOrderNumber
        }).IsUnique();

        builder.HasOne(x => x.ConstructionSite)
            .WithMany(x => x.PurchaseOrders)
            .HasForeignKey(x => x.ConstructionSiteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Project)
            .WithMany(x => x.PurchaseOrders)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Vendor)
            .WithMany(x => x.PurchaseOrders)
            .HasForeignKey(x => x.VendorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.MaterialRequest)
            .WithMany()
            .HasForeignKey(x => x.MaterialRequestId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}