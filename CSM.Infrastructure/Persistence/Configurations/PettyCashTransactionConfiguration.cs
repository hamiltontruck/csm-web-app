using CSM.Domain.Entities.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace CSM.Infrastructure.Persistence.Configurations;
public sealed class PettyCashTransactionConfiguration : IEntityTypeConfiguration<PettyCashTransaction>
{
    public void Configure(EntityTypeBuilder<PettyCashTransaction> builder)
    {
        builder.HasOne(x => x.Expense).WithMany().HasForeignKey(x => x.ExpenseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.PettyCashAccountId);
        builder.HasIndex(x => new { x.PettyCashAccountId, x.ReferenceNumber }).IsUnique().HasFilter("\"ExpenseId\" IS NOT NULL");
    }
}
