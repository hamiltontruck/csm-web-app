using CSM.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CSM.Infrastructure.Persistence.Configurations;

public sealed class PrivacyDataRequestConfiguration : IEntityTypeConfiguration<PrivacyDataRequest>
{
    public void Configure(EntityTypeBuilder<PrivacyDataRequest> builder)
    {
        builder.ToTable("PrivacyDataRequests");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RequestType).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.ResolutionNotes).HasMaxLength(2000);
        builder.HasIndex(x => new { x.CompanyId, x.UserId, x.RequestedAtUtc });
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
