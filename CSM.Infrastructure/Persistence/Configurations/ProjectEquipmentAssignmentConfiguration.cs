using CSM.Domain.Entities.Organization;
using CSM.Domain.Entities.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CSM.Infrastructure.Persistence.Configurations;

public sealed class ProjectEquipmentAssignmentConfiguration
    : IEntityTypeConfiguration<EquipmentAssignment>
{
    public void Configure(EntityTypeBuilder<EquipmentAssignment> builder)
    {
        builder.ToTable("ProjectEquipmentAssignments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.AssignedDate)
            .IsRequired();

        builder.Property(x => x.OperatorName)
            .HasMaxLength(150);

        builder.Property(x => x.Remarks)
            .HasMaxLength(500);

        builder.HasOne(x => x.Equipment)
            .WithMany(x => x.Assignments)
            .HasForeignKey(x => x.EquipmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ConstructionSite>()
            .WithMany(x => x.EquipmentAssignments)
            .HasForeignKey(x => x.ConstructionSiteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}