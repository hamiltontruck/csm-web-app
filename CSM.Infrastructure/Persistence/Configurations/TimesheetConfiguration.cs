using CSM.Domain.Entities.HRM;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CSM.Infrastructure.Persistence.Configurations;

public class TimesheetConfiguration :
    IEntityTypeConfiguration<Timesheet>
{
    public void Configure(EntityTypeBuilder<Timesheet> builder)
    {
        builder.ToTable("Timesheets");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.RegularHours)
            .HasPrecision(10, 2);

        builder.Property(x => x.OvertimeHours)
            .HasPrecision(10, 2);

        builder.Property(x => x.TotalHours)
            .HasPrecision(10, 2);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(x => x.ApprovalRemarks)
            .HasMaxLength(1000);

        builder.Property(x => x.Remarks)
            .HasMaxLength(1000);

        builder.HasIndex(x => new
        {
            x.CompanyId,
            x.EmployeeId,
            x.PeriodStartDate,
            x.PeriodEndDate
        });

        builder.HasOne(x => x.Employee)
            .WithMany(x => x.Timesheets)
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
