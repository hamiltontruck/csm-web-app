using CSM.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CSM.Infrastructure.Persistence.Configurations;

public sealed class RoleGranularPermissionConfiguration
    : IEntityTypeConfiguration<RoleGranularPermission>
{
    public void Configure(EntityTypeBuilder<RoleGranularPermission> builder)
    {
        builder.ToTable("RoleGranularPermissions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Permission)
            .HasConversion<string>()
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(x => new { x.RoleId, x.Permission })
            .IsUnique();

        builder.HasOne(x => x.Role)
            .WithMany(x => x.GranularPermissions)
            .HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}