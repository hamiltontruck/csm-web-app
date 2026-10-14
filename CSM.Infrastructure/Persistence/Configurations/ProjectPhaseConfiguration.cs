using CSM.Domain.Entities.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CSM.Infrastructure.Persistence.Configurations;

public class ProjectPhaseConfiguration :
    IEntityTypeConfiguration<ProjectPhase>
{
    public void Configure(EntityTypeBuilder<ProjectPhase> builder)
    {
        builder.ToTable("ProjectPhases");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.ProgressPercentage)
            .HasPrecision(5, 2);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(30);

         builder.Property(x => x.PhaseNumber)
             .HasMaxLength(30)
             .IsRequired();

         builder.HasIndex(x => new
         {
             x.CompanyId,
             x.PhaseNumber
         }).IsUnique();

        builder.HasIndex(x => new
        {
            x.ProjectId,
            x.Sequence
        }).IsUnique();

        builder.HasOne(x => x.Project)
            .WithMany(x => x.Phases)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}