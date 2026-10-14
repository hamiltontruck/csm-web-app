using CSM.Domain.Entities.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CSM.Infrastructure.Persistence.Configurations;

public class WorkTaskDependencyConfiguration :
    IEntityTypeConfiguration<WorkTaskDependency>
{
    public void Configure(EntityTypeBuilder<WorkTaskDependency> builder)
    {
        builder.ToTable("WorkTaskDependencies");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new
        {
            x.WorkTaskId,
            x.DependsOnTaskId
        }).IsUnique();

        builder.HasOne(x => x.WorkTask)
            .WithMany(x => x.Dependencies)
            .HasForeignKey(x => x.WorkTaskId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.DependsOnTask)
            .WithMany(x => x.DependentTasks)
            .HasForeignKey(x => x.DependsOnTaskId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}