using CSM.Domain.Entities.Compliance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CSM.Infrastructure.Persistence.Configurations;

public class DocumentConfiguration :
    IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("Documents");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.DocumentNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(250)
            .IsRequired();

        builder.Property(x => x.FileName)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.StoragePath)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(x => x.ContentType).HasMaxLength(150);

        builder.Property(x => x.DocumentType)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(x => x.RelatedEntityType)
            .HasMaxLength(100);

        builder.HasIndex(x => new
        {
            x.CompanyId,
            x.DocumentNumber
        }).IsUnique();

        builder.HasIndex(x => new
        {
            x.RelatedEntityType,
            x.RelatedEntityId
        });

        builder.HasIndex(x => x.ExpiryDate);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}