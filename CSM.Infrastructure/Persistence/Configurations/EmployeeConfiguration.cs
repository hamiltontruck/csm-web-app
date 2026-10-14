using CSM.Domain.Entities.HRM;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CSM.Infrastructure.Persistence.Configurations;

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employees");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.EmployeeNumber)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.FirstName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.MiddleName)
            .HasMaxLength(100);

        builder.Property(x => x.LastName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Email).HasMaxLength(200);
        builder.Property(x => x.PhoneNumber).HasMaxLength(50);
        builder.Property(x => x.JobTitle).HasMaxLength(150);
        builder.Property(x => x.Department).HasMaxLength(150);

        builder.Property(x => x.EmployeeType)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(x => x.EmployeeScope)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(x => x.WageType)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.BaseWage)
            .HasPrecision(19, 4);

        builder.Property(x => x.CurrencyCode)
            .HasMaxLength(3);

        builder.HasIndex(x => new { x.CompanyId, x.EmployeeNumber })
            .IsUnique();

        builder.HasIndex(x => new { x.CompanyId, x.Status });

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}