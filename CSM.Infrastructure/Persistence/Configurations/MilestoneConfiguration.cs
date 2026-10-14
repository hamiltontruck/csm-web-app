using CSM.Domain.Entities.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CSM.Infrastructure.Persistence.Configurations;

public class MilestoneConfiguration :
    IEntityTypeConfiguration<Milestone>
{
    public void Configure(EntityTypeBuilder<Milestone> builder)
    {
        builder.ToTable("Milestones");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

         builder.Property(x => x.MilestoneNumber)
             .HasMaxLength(30)
             .IsRequired();

         builder.HasIndex(x => new
         {
             x.CompanyId,
             x.MilestoneNumber
         }).IsUnique();

        builder.HasIndex(x => x.ProjectId);

        builder.HasOne(x => x.Project)
            .WithMany(x => x.Milestones)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProjectPhase)
            .WithMany(x => x.Milestones)
            .HasForeignKey(x => x.ProjectPhaseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}