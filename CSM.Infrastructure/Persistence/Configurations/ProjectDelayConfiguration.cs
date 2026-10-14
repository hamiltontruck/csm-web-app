using CSM.Domain.Entities.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CSM.Infrastructure.Persistence.Configurations;

public class ProjectDelayConfiguration : IEntityTypeConfiguration<ProjectDelay>
{
    public void Configure(EntityTypeBuilder<ProjectDelay> builder)
    {
        builder.ToTable("ProjectDelays");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Category)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(x => x.ActionTaken)
            .HasMaxLength(1000);

        builder.HasOne(x => x.Project)
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new
        {
            x.CompanyId,
            x.ProjectId,
            x.DelayDate
        });
    }
}