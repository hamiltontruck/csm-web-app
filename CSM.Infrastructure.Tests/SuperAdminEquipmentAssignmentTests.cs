using CSM.Application.Common.Exceptions;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Entities.Procurement;
using CSM.Domain.Enums;
using CSM.Infrastructure.Services;
using Xunit;
namespace CSM.Infrastructure.Tests;
public sealed class SuperAdminEquipmentAssignmentTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Superadmin_assigns_with_equipment_company_without_changing_profile(bool assigned)
    {
        using var f=new SuperAdminEquipmentCreateTests.Fixture("SuperAdmin",assigned);
        var equipment=Equipment(f.Beta.Id);var site=Site(f.Beta.Id);f.Db.AddRange(equipment,site);await f.Db.SaveChangesAsync();
        var row=await new EquipmentAssignmentService(f.Db).CreateAsync(f.Actor.Id,new(equipment.Id,site.Id,null,DateTime.UtcNow,2,"assignment test"));
        Assert.Equal(f.Beta.Id,row.CompanyId);Assert.Equal(f.Beta.Id,f.Db.EquipmentAssignments.Single().CompanyId);
        Assert.Equal(EquipmentStatus.Assigned,equipment.Status);Assert.Equal(2,equipment.CurrentMeterReading);
        Assert.Equal(assigned?f.Alpha.Id:(Guid?)null,f.Actor.CompanyId);
    }
    [Fact]
    public async Task Superadmin_cannot_mix_foreign_equipment_with_own_company_site()
    {
        using var f=new SuperAdminEquipmentCreateTests.Fixture("SuperAdmin",true);
        var equipment=Equipment(f.Beta.Id);var site=Site(f.Alpha.Id);f.Db.AddRange(equipment,site);await f.Db.SaveChangesAsync();
        var error=await Assert.ThrowsAsync<EquipmentAssignmentManagementException>(()=>new EquipmentAssignmentService(f.Db).CreateAsync(f.Actor.Id,new(equipment.Id,site.Id,null,DateTime.UtcNow,2,null)));
        Assert.Contains("same company",error.Message);Assert.Empty(f.Db.EquipmentAssignments);Assert.Equal(EquipmentStatus.Available,equipment.Status);Assert.Equal(0,equipment.CurrentMeterReading);
    }
    [Theory]
    [InlineData("CompanyAdmin")] [InlineData("SiteManager")]
    public async Task Company_writer_cannot_assign_foreign_equipment(string role)
    {
        using var f=new SuperAdminEquipmentCreateTests.Fixture(role,true);
        var equipment=Equipment(f.Beta.Id);var site=Site(f.Beta.Id);f.Db.AddRange(equipment,site);await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<EquipmentAssignmentManagementException>(()=>new EquipmentAssignmentService(f.Db).CreateAsync(f.Actor.Id,new(equipment.Id,site.Id,null,DateTime.UtcNow,2,null)));
        Assert.Empty(f.Db.EquipmentAssignments);Assert.Equal(EquipmentStatus.Available,equipment.Status);
    }
    [Fact]
    public async Task Existing_company_writer_still_assigns_own_equipment_and_duplicate_is_blocked()
    {
        using var f=new SuperAdminEquipmentCreateTests.Fixture("CompanyAdmin",true);
        var equipment=Equipment(f.Alpha.Id);var site=Site(f.Alpha.Id);f.Db.AddRange(equipment,site);await f.Db.SaveChangesAsync();
        var service=new EquipmentAssignmentService(f.Db);await service.CreateAsync(f.Actor.Id,new(equipment.Id,site.Id,null,DateTime.UtcNow,2,null));
        await Assert.ThrowsAsync<EquipmentAssignmentManagementException>(()=>service.CreateAsync(f.Actor.Id,new(equipment.Id,site.Id,null,DateTime.UtcNow,3,null)));
        Assert.Single(f.Db.EquipmentAssignments);Assert.Equal(2,equipment.CurrentMeterReading);
    }
    private static Equipment Equipment(Guid company)=>new(){CompanyId=company,EquipmentCode="EQ-ASSIGN",Name="Compactor",IsActive=true,Status=EquipmentStatus.Available};
    private static ConstructionSite Site(Guid company)=>new(){CompanyId=company,SiteCode="SITE-ASSIGN",Name="Assignment site",IsActive=true};
}
