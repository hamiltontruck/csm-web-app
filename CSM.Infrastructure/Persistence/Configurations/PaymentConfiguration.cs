using CSM.Domain.Entities.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CSM.Infrastructure.Persistence.Configurations;

public class PaymentConfiguration :
    IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.PaymentNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Amount).HasPrecision(19, 4);
        builder.Property(x => x.ExchangeRate).HasPrecision(19, 8);

        builder.Property(x => x.CurrencyCode)
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(x => x.Direction)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.PaymentMethod).HasMaxLength(50);
        builder.Property(x => x.ReferenceNumber).HasMaxLength(100);

        builder.HasIndex(x => new { x.CompanyId, x.PaymentNumber })
            .IsUnique();

        builder.HasOne(x => x.Invoice)
            .WithMany(x => x.Payments)
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Vendor)
            .WithMany()
            .HasForeignKey(x => x.VendorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.PayrollRecord)
            .WithMany()
            .HasForeignKey(x => x.PayrollRecordId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}