
using CSM.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CSM.Infrastructure.Persistence.Configurations;

public sealed class UserPreferencesConfiguration
    : IEntityTypeConfiguration<UserPreferences>
{
    public void Configure(
        EntityTypeBuilder<UserPreferences> builder)
    {
        builder.ToTable("UserPreferences");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.UserId)
            .IsUnique();

        builder.Property(x => x.Theme)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Language)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(x => x.TimeZone)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.DateFormat)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.TimeFormat)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(x => x.NumberFormat)
            .HasMaxLength(20)
            .IsRequired();

        builder.HasOne(x => x.User)
            .WithOne()
            .HasForeignKey<UserPreferences>(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}