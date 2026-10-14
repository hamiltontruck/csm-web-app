using CSM.Domain.Entities.Compliance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CSM.Infrastructure.Persistence.Configurations;

public sealed class DocumentCategoryConfiguration
    : IEntityTypeConfiguration<DocumentCategory>
{
    public void Configure(
        EntityTypeBuilder<DocumentCategory> builder)
    {
        builder.ToTable("DocumentCategories");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CategoryCode)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(1000);

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.HasIndex(x =>
            new
            {
                x.CompanyId,
                x.CategoryCode
            })
            .IsUnique();
    }
}



