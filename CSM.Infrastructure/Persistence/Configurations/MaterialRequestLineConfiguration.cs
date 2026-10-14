using CSM.Domain.Entities.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CSM.Infrastructure.Persistence.Configurations;

public class MaterialRequestLineConfiguration :
    IEntityTypeConfiguration<MaterialRequestLine>
{
    public void Configure(EntityTypeBuilder<MaterialRequestLine> builder)
    {
        builder.ToTable("MaterialRequestLines");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.RequestedQuantity).HasPrecision(19, 4);
        builder.Property(x => x.ApprovedQuantity).HasPrecision(19, 4);
        builder.Property(x => x.DeliveredQuantity).HasPrecision(19, 4);

        builder.HasIndex(x => new
        {
            x.MaterialRequestId,
            x.MaterialId
        }).IsUnique();

        builder.HasOne(x => x.MaterialRequest)
            .WithMany(x => x.Lines)
            .HasForeignKey(x => x.MaterialRequestId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Material)
            .WithMany(x => x.MaterialRequestLines)
            .HasForeignKey(x => x.MaterialId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}