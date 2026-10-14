using CSM.Domain.Entities.HRM;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CSM.Infrastructure.Persistence.Configurations;

public class PayrollRecordConfiguration :
    IEntityTypeConfiguration<PayrollRecord>
{
    public void Configure(EntityTypeBuilder<PayrollRecord> builder)
    {
        builder.ToTable("PayrollRecords");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(x => x.CurrencyCode)
            .HasMaxLength(3);

        var moneyProperties = new[]
        {
            nameof(PayrollRecord.BasePay),
            nameof(PayrollRecord.OvertimePay),
            nameof(PayrollRecord.Allowances),
            nameof(PayrollRecord.Bonuses),
            nameof(PayrollRecord.GrossPay),
            nameof(PayrollRecord.TaxDeduction),
            nameof(PayrollRecord.PensionDeduction),
            nameof(PayrollRecord.OtherDeductions),
            nameof(PayrollRecord.TotalDeductions),
            nameof(PayrollRecord.NetPay)
        };

        foreach (var property in moneyProperties)
        {
            builder.Property<decimal>(property)
                .HasPrecision(19, 4);
        }

        builder.Property(x => x.RegularHours).HasPrecision(8, 2);
        builder.Property(x => x.OvertimeHours).HasPrecision(8, 2);

        builder.HasIndex(x => new
        {
            x.EmployeeId,
            x.PeriodStart,
            x.PeriodEnd
        }).IsUnique();

        builder.HasOne(x => x.Employee)
            .WithMany(x => x.PayrollRecords)
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}