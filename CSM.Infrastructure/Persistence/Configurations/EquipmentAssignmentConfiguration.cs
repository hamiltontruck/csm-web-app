using CSM.Domain.Entities.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CSM.Infrastructure.Persistence.Configurations;

public sealed class EquipmentAssignmentConfiguration
    : IEntityTypeConfiguration<EquipmentAssignment>
{
    public void Configure(EntityTypeBuilder<EquipmentAssignment> builder)
    {
        builder.ToTable("EquipmentAssignments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.AssignmentNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.AssignedAtUtc)
            .IsRequired();

        builder.Property(x => x.Remarks)
            .HasMaxLength(500);

        builder.Property(x => x.MeterReadingAtAssignment)
            .HasPrecision(18, 2);

        builder.Property(x => x.MeterReadingAtRelease)
            .HasPrecision(18, 2);

        builder.HasOne(x => x.Equipment)
            .WithMany(x => x.Assignments)
            .HasForeignKey(x => x.EquipmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ConstructionSite)
            .WithMany()
            .HasForeignKey(x => x.ConstructionSiteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Project)
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}