using CSM.Domain.Entities.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CSM.Infrastructure.Persistence.Configurations;

public sealed class WbsTaskConfiguration
    : IEntityTypeConfiguration<WbsTask>
{
    public void Configure(EntityTypeBuilder<WbsTask> builder)
    {
        builder.ToTable("WbsTasks");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TaskCode)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(2000);

        builder.Property(x => x.ProgressPercentage)
            .HasPrecision(5, 2);

        builder.Property(x => x.EstimatedHours)
            .HasPrecision(10, 2);

        builder.Property(x => x.ActualHours)
            .HasPrecision(10, 2);

        builder.HasOne(x => x.Project)
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Milestone)
            .WithMany()
            .HasForeignKey(x => x.MilestoneId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.AssignedEmployee)
            .WithMany()
            .HasForeignKey(x => x.AssignedEmployeeId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => new
        {
            x.CompanyId,
            x.ProjectId,
            x.TaskCode
        }).IsUnique();
    }
}