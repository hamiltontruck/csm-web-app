using CSM.Domain.Entities.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CSM.Infrastructure.Persistence.Configurations;

public class InvoiceConfiguration :
    IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("Invoices");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.InvoiceNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Type)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(x => x.Subtotal).HasPrecision(19, 4);
        builder.Property(x => x.TaxAmount).HasPrecision(19, 4);
        builder.Property(x => x.TotalAmount).HasPrecision(19, 4);
        builder.Property(x => x.ExchangeRate).HasPrecision(19, 8);

        builder.Property(x => x.CurrencyCode)
            .HasMaxLength(3);

        builder.HasIndex(x => new
        {
            x.CompanyId,
            x.InvoiceNumber
        }).IsUnique();

        builder.HasOne(x => x.ConstructionSite)
            .WithMany(x => x.Invoices)
            .HasForeignKey(x => x.ConstructionSiteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Project)
            .WithMany(x => x.Invoices)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Vendor)
            .WithMany(x => x.Invoices)
            .HasForeignKey(x => x.VendorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}