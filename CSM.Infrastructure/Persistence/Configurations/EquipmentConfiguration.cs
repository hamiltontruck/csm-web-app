using CSM.Domain.Entities.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CSM.Infrastructure.Persistence.Configurations;

public class EquipmentConfiguration :
    IEntityTypeConfiguration<Equipment>
{
    public void Configure(EntityTypeBuilder<Equipment> builder)
    {
        builder.ToTable("Equipment");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.EquipmentCode)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.OwnershipType)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(x => x.PurchaseCost).HasPrecision(19, 4);
        builder.Property(x => x.RentalRate).HasPrecision(19, 4);
        builder.Property(x => x.CurrentMeterReading).HasPrecision(19, 4);

        builder.HasIndex(x => new
        {
            x.CompanyId,
            x.EquipmentCode
        }).IsUnique();

        builder.HasOne(x => x.Vendor)
            .WithMany(v => v.Equipments)
            .HasForeignKey(x => x.VendorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}