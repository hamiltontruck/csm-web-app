using CSM.Domain.Entities.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CSM.Infrastructure.Persistence.Configurations;

public class ExpenseConfiguration :
    IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.ToTable("Expenses");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ExpenseNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.Amount).HasPrecision(19, 4);
        builder.Property(x => x.TaxAmount).HasPrecision(19, 4);
        builder.Property(x => x.ExchangeRate).HasPrecision(19, 8);

        builder.Property(x => x.CurrencyCode)
            .HasMaxLength(3);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.HasIndex(x => new { x.CompanyId, x.ExpenseNumber })
            .IsUnique();

        builder.HasIndex(x => new
        {
            x.ConstructionSiteId,
            x.CostCodeId,
            x.ExpenseDate
        });

        builder.HasOne(x => x.ConstructionSite)
            .WithMany(x => x.Expenses)
            .HasForeignKey(x => x.ConstructionSiteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Project)
            .WithMany(x => x.Expenses)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CostCode)
            .WithMany(x => x.Expenses)
            .HasForeignKey(x => x.CostCodeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Vendor)
            .WithMany(x => x.Expenses)
            .HasForeignKey(x => x.VendorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}