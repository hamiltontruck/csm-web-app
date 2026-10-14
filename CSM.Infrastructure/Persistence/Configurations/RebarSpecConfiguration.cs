using CSM.Domain.Entities.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CSM.Infrastructure.Persistence.Configurations;

public class RebarSpecConfiguration :
    IEntityTypeConfiguration<RebarSpec>
{
    public void Configure(
        EntityTypeBuilder<RebarSpec> builder)
    {
        builder.ToTable("RebarSpecs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.DiameterMm)
            .HasPrecision(10, 2);

        builder.Property(x => x.Grade)
            .HasMaxLength(30)
            .IsRequired();

        builder.HasIndex(x => x.MaterialId)
            .IsUnique();

        builder.HasOne(x => x.Material)
            .WithOne(x => x.RebarSpec)
            .HasForeignKey<RebarSpec>(x => x.MaterialId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
