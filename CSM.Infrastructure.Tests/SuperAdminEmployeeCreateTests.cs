using CSM.Application.Common.Exceptions;
using CSM.Application.HRM.Employees.Dtos;
using CSM.Domain.Enums;
using CSM.Infrastructure.Services;
using Xunit;
namespace CSM.Infrastructure.Tests;
public sealed class SuperAdminEmployeeCreateTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Superadmin_can_choose_company_without_reassigning_profile(bool assigned)
    {
        using var f=new SuperAdminEquipmentCreateTests.Fixture("SuperAdmin",assigned);
        var row=await new EmployeeService(f.Db).CreateAsync(f.Actor.Id,Request(f.Beta.Id));
        Assert.Equal(f.Beta.Id,row.CompanyId);Assert.Equal(f.Beta.Id,f.Db.Employees.Single().CompanyId);
        Assert.Equal(assigned?f.Alpha.Id:(Guid?)null,f.Actor.CompanyId);
    }
    [Theory]
    [InlineData("CompanyAdmin")] [InlineData("HrOfficer")] [InlineData("HROfficer")]
    public async Task Company_writer_cannot_forge_another_company(string role)
    {
        using var f=new SuperAdminEquipmentCreateTests.Fixture(role,true);
        await Assert.ThrowsAsync<EmployeeManagementException>(()=>new EmployeeService(f.Db).CreateAsync(f.Actor.Id,Request(f.Beta.Id)));
        Assert.Empty(f.Db.Employees);
    }
    [Theory]
    [InlineData("missing")] [InlineData("inactive")] [InlineData("deleted")] [InlineData("empty")]
    public async Task Invalid_company_is_rejected_before_employee_creation(string kind)
    {
        using var f=new SuperAdminEquipmentCreateTests.Fixture("SuperAdmin",false);
        if(kind=="inactive") f.Beta.IsActive=false;if(kind=="deleted") f.Beta.IsDeleted=true;
        await f.Db.SaveChangesAsync();
        var company=kind=="missing"?Guid.NewGuid():kind=="empty"?Guid.Empty:f.Beta.Id;
        await Assert.ThrowsAsync<EmployeeManagementException>(()=>new EmployeeService(f.Db).CreateAsync(f.Actor.Id,Request(company)));
        Assert.Empty(f.Db.Employees);
    }
    [Fact]
    public async Task Missing_company_has_actionable_message()
    {
        using var f=new SuperAdminEquipmentCreateTests.Fixture("SuperAdmin",false);
        var error=await Assert.ThrowsAsync<EmployeeManagementException>(()=>new EmployeeService(f.Db).CreateAsync(f.Actor.Id,Request(null)));
        Assert.Contains("Select a company",error.Message);
    }
    [Fact]
    public async Task Employee_number_uniqueness_uses_selected_company()
    {
        using var f=new SuperAdminEquipmentCreateTests.Fixture("SuperAdmin",false);var service=new EmployeeService(f.Db);
        await service.CreateAsync(f.Actor.Id,Request(f.Alpha.Id));await service.CreateAsync(f.Actor.Id,Request(f.Beta.Id));
        await Assert.ThrowsAsync<EmployeeManagementException>(()=>service.CreateAsync(f.Actor.Id,Request(f.Beta.Id)));
        Assert.Equal(2,f.Db.Employees.Count());
    }
    [Fact]
    public async Task Legacy_company_payload_uses_own_company()
    {
        using var f=new SuperAdminEquipmentCreateTests.Fixture("CompanyAdmin",true);
        var row=await new EmployeeService(f.Db).CreateAsync(f.Actor.Id,LegacyRequest());Assert.Equal(f.Alpha.Id,row.CompanyId);
    }
    [Theory]
    [InlineData("SuperAdmin", 2, true)] [InlineData("CompanyAdmin", 0, false)] [InlineData("HrOfficer", 0, false)]
    public async Task Options_are_minimal_and_company_scoped(string role,int count,bool choose)
    {
        using var f=new SuperAdminEquipmentCreateTests.Fixture(role,!choose);
        f.Db.Companies.Add(new(){Code="HIDDEN",Name="Hidden",IsActive=false});await f.Db.SaveChangesAsync();
        var options=await new EmployeeService(f.Db).GetCreateOptionsAsync(f.Actor.Id);
        Assert.Equal(choose,options.CanSelectCompany);Assert.Equal(count,options.Companies.Count);
        Assert.DoesNotContain(options.Companies,x=>x.Code=="HIDDEN");
        if(choose)Assert.Null(options.CompanyId);else Assert.Equal(f.Alpha.Id,options.CompanyId);
    }
    private static CreateEmployeeRequest LegacyRequest()=>new("EMP-LOCAL","Comp",null,"Level",null,null,null,null,null,new DateOnly(2026,11,13),null,null,EmployeeType.Permanent,EmployeeScope.Company,WageType.Monthly,1000,"ETB",null,null,null,null);
    private static CreateEmployeeRequest Request(Guid? company)
    {
        return LegacyRequest() with { CompanyId = company };
    }
}
