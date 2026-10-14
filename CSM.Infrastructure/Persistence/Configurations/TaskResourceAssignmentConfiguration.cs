using CSM.Domain.Entities.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CSM.Infrastructure.Persistence.Configurations.Projects;

public sealed class TaskResourceAssignmentConfiguration
    : IEntityTypeConfiguration<TaskResourceAssignment>
{
    public void Configure(
        EntityTypeBuilder<TaskResourceAssignment> builder)
    {
        builder.ToTable("TaskResourceAssignments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Role)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Remarks)
            .HasMaxLength(500);

        builder.Property(x => x.PlannedHours)
            .HasPrecision(10, 2);

        builder.Property(x => x.ActualHours)
            .HasPrecision(10, 2);

        builder.HasOne(x => x.WbsTask)
            .WithMany(x => x.ResourceAssignments)
            .HasForeignKey(x => x.WbsTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Employee)
            .WithMany()
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}