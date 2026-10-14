using CSM.Domain.Entities.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CSM.Infrastructure.Persistence.Configurations;

public class MaterialConfiguration : IEntityTypeConfiguration<Material>
{
    public void Configure(EntityTypeBuilder<Material> builder)
    {
        builder.ToTable("Materials");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.MaterialCode)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Category).HasMaxLength(100);

        builder.Property(x => x.MaterialType)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(x => x.UnitOfMeasure)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.StandardUnitCost)
            .HasPrecision(19, 4);

        builder.HasIndex(x => new { x.CompanyId, x.MaterialCode })
            .IsUnique();

        builder.HasOne(x => x.DefaultCostCode)
            .WithMany()
            .HasForeignKey(x => x.DefaultCostCodeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
