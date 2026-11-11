using CSM.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace CSM.Infrastructure.Persistence.Configurations;
public sealed class LoginHistoryEntryConfiguration : IEntityTypeConfiguration<LoginHistoryEntry>
{
    public void Configure(EntityTypeBuilder<LoginHistoryEntry> builder)
    {
        builder.ToTable("LoginHistoryEntries"); builder.HasKey(x => x.Id);
        builder.Property(x => x.IpAddress).HasMaxLength(64);
        builder.Property(x => x.DeviceName).HasMaxLength(256);
        builder.Property(x => x.FailureReason).HasMaxLength(200);
        builder.HasIndex(x => new { x.UserId, x.OccurredAtUtc });
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
