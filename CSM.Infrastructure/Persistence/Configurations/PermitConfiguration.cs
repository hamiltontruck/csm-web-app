using CSM.Domain.Entities.Compliance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CSM.Infrastructure.Persistence.Configurations;

public class PermitConfiguration : IEntityTypeConfiguration<Permit>
{
    public void Configure(EntityTypeBuilder<Permit> builder)
    {
        builder.ToTable("Permits");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.PermitNumber)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.PermitType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.HasIndex(x => new
        {
            x.CompanyId,
            x.PermitNumber
        }).IsUnique();

        builder.HasIndex(x => x.ExpiryDate);

        builder.HasOne(x => x.ConstructionSite)
            .WithMany(x => x.Permits)
            .HasForeignKey(x => x.ConstructionSiteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Project)
            .WithMany(x => x.Permits)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}