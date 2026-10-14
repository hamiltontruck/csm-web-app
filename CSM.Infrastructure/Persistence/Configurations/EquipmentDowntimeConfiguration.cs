using CSM.Domain.Entities.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CSM.Infrastructure.Persistence.Configurations;

public class EquipmentDowntimeConfiguration :
    IEntityTypeConfiguration<EquipmentDowntime>
{
    public void Configure(EntityTypeBuilder<EquipmentDowntime> builder)
    {
        builder.ToTable("EquipmentDowntime");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Reason)
            .HasMaxLength(500)
            .IsRequired();
         builder.Property(x => x.DowntimeNumber)
             .HasMaxLength(30)
             .IsRequired();

         builder.HasIndex(x => new
         {
             x.CompanyId,
             x.DowntimeNumber
         }).IsUnique();



        builder.HasOne(x => x.Equipment)
            .WithMany(x => x.DowntimeRecords)
            .HasForeignKey(x => x.EquipmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ConstructionSite)
            .WithMany(x => x.EquipmentDowntimeRecords)
            .HasForeignKey(x => x.ConstructionSiteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}