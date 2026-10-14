using CSM.Domain.Entities.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CSM.Infrastructure.Persistence.Configurations;

public class ConstructionSiteConfiguration :
    IEntityTypeConfiguration<ConstructionSite>
{
    public void Configure(EntityTypeBuilder<ConstructionSite> builder)
    {
        builder.ToTable("ConstructionSites");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SiteCode)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(x => x.Latitude)
            .HasPrecision(10, 7);

        builder.Property(x => x.Longitude)
            .HasPrecision(10, 7);

        builder.Property(x => x.GeofenceRadiusMeters)
            .HasPrecision(10, 2);

        builder.HasIndex(x => new
        {
            x.CompanyId,
            x.SiteCode
        }).IsUnique();

        builder.HasIndex(x => new
        {
            x.CompanyId,
            x.Status
        });

        builder.HasOne(x => x.Company)
            .WithMany(x => x.ConstructionSites)
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.BusinessUnit)
            .WithMany(x => x.ConstructionSites)
            .HasForeignKey(x => x.BusinessUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}