using CSM.Domain.Entities.HRM;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CSM.Infrastructure.Persistence.Configurations;

public class ContractorConfiguration : IEntityTypeConfiguration<Contractor>
{
    public void Configure(EntityTypeBuilder<Contractor> builder)
    {
        builder.ToTable("Contractors");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ContractorCode)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.ContactPerson).HasMaxLength(150);
        builder.Property(x => x.PhoneNumber).HasMaxLength(50);
        builder.Property(x => x.Email).HasMaxLength(200);

        builder.HasIndex(x => new { x.CompanyId, x.ContractorCode })
            .IsUnique();

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}