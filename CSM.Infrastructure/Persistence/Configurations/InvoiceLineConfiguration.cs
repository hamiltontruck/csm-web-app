using CSM.Domain.Entities.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CSM.Infrastructure.Persistence.Configurations;

public class InvoiceLineConfiguration :
    IEntityTypeConfiguration<InvoiceLine>
{
    public void Configure(EntityTypeBuilder<InvoiceLine> builder)
    {
        builder.ToTable("InvoiceLines");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Description)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.Quantity).HasPrecision(19, 4);
        builder.Property(x => x.UnitPrice).HasPrecision(19, 4);
        builder.Property(x => x.TaxAmount).HasPrecision(19, 4);
        builder.Property(x => x.LineTotal).HasPrecision(19, 4);

        builder.HasOne(x => x.Invoice)
            .WithMany(x => x.Lines)
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CostCode)
            .WithMany()
            .HasForeignKey(x => x.CostCodeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasOne(x => x.CostCode)
            .WithMany(x => x.InvoiceLines)
            .HasForeignKey(x => x.CostCodeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}