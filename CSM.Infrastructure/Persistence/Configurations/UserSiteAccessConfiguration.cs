using CSM.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CSM.Infrastructure.Persistence.Configurations;

public class UserSiteAccessConfiguration
    : IEntityTypeConfiguration<UserSiteAccess>
{
    public void Configure(EntityTypeBuilder<UserSiteAccess> builder)
    {
        builder.ToTable("UserSiteAccesses");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new
        {
            x.CompanyId,
            x.UserId,
            x.SiteId
        }).IsUnique();

        builder.HasOne(x => x.User)
            .WithMany(x => x.SiteAccesses)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Site)
            .WithMany()
            .HasForeignKey(x => x.SiteId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}