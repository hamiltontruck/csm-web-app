using CSM.Domain.Entities.HRM;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CSM.Infrastructure.Persistence.Configurations;

public class SiteAssignmentConfiguration :
    IEntityTypeConfiguration<SiteAssignment>
{
    public void Configure(EntityTypeBuilder<SiteAssignment> builder)
    {
        builder.ToTable("SiteAssignments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.RoleAtSite)
            .HasMaxLength(150);

        builder.HasIndex(x => new
        {
            x.EmployeeId,
            x.ConstructionSiteId,
            x.StartDate
        });

        builder.HasOne(x => x.Employee)
            .WithMany(x => x.SiteAssignments)
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ConstructionSite)
            .WithMany(x => x.SiteAssignments)
            .HasForeignKey(x => x.ConstructionSiteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}