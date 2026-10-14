using CSM.Domain.Entities.Compliance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CSM.Infrastructure.Persistence.Configurations;

public class InspectionChecklistItemConfiguration :
    IEntityTypeConfiguration<InspectionChecklistItem>
{
    public void Configure(EntityTypeBuilder<InspectionChecklistItem> builder)
    {
        builder.ToTable("InspectionChecklistItems");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Requirement)
            .HasMaxLength(500)
            .IsRequired();

        builder.HasIndex(x => new
        {
            x.InspectionId,
            x.Sequence
        }).IsUnique();

        builder.HasOne(x => x.Inspection)
            .WithMany(x => x.ChecklistItems)
            .HasForeignKey(x => x.InspectionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}