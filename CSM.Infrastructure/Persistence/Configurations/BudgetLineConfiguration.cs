using CSM.Domain.Entities.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CSM.Infrastructure.Persistence.Configurations;

public class BudgetLineConfiguration :
    IEntityTypeConfiguration<BudgetLine>
{
    public void Configure(EntityTypeBuilder<BudgetLine> builder)
    {
        builder.ToTable("BudgetLines");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Description).HasMaxLength(300);

        builder.Property(x => x.BudgetedAmount)
            .HasPrecision(19, 4);

        builder.Property(x => x.RevisedAmount)
            .HasPrecision(19, 4);

        builder.HasIndex(x => new
        {
            x.BudgetId,
            x.CostCodeId
        })
        .IsUnique()
        .HasFilter("[IsDeleted] = 0");

        builder.HasOne(x => x.Budget)
            .WithMany(x => x.Lines)
            .HasForeignKey(x => x.BudgetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CostCode)
            .WithMany(x => x.BudgetLines)
            .HasForeignKey(x => x.CostCodeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}