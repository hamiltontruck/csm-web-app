using System.Reflection;
using System.Text.Json;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace CSM.Infrastructure.Tests;
public sealed class CompanyContextResolverTests
{
    [Fact]
    public async Task Global_selection_uses_validated_selected_company_without_account_assignment()
    {
        using var f=new Fixture();Assert.Equal(f.Other.Id,await Resolve(f,null,true,f.Other.Id));
        Assert.Equal(f.Other.Id,await Resolve(f,f.Own.Id,true,f.Other.Id));Assert.Equal(f.Own.Id,await Resolve(f,f.Own.Id,true,null));
    }
    [Fact]
    public async Task Missing_global_selection_is_actionable_and_never_picks_first_company()
    {
        using var f=new Fixture();var error=await Assert.ThrowsAsync<InvalidOperationException>(()=>Resolve(f,null,true,null));Assert.Contains("Select a company",error.Message);
    }
    [Theory] [InlineData("missing")] [InlineData("inactive")] [InlineData("deleted")] [InlineData("empty")]
    public async Task Invalid_company_is_rejected(string invalid)
    {
        using var f=new Fixture();var id=invalid switch{"inactive"=>f.Inactive.Id,"deleted"=>f.Deleted.Id,"empty"=>Guid.Empty,_=>Guid.NewGuid()};
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Resolve(f,null,true,id));
    }
    [Fact]
    public async Task Ordinary_company_context_cannot_select_another_company()
    {
        using var f=new Fixture();Assert.Equal(f.Own.Id,await Resolve(f,f.Own.Id,false,null));Assert.Equal(f.Own.Id,await Resolve(f,f.Own.Id,false,f.Own.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Resolve(f,f.Own.Id,false,f.Other.Id));await Assert.ThrowsAsync<InvalidOperationException>(()=>Resolve(f,null,false,f.Other.Id));
    }
    [Fact]
    public async Task Options_are_minimal_active_choices_and_keep_ordinary_company_private()
    {
        using var f=new Fixture();var value=await Result("OptionsAsync",f.Db,null,true,Error,default(CancellationToken));
        using var json=JsonDocument.Parse(JsonSerializer.Serialize(value,value.GetType()));var root=json.RootElement;
        Assert.True(root.GetProperty("CanSelectCompany").GetBoolean());Assert.Equal(JsonValueKind.Null,root.GetProperty("CompanyId").ValueKind);
        var rows=root.GetProperty("Companies").EnumerateArray().ToArray();Assert.Equal(2,rows.Length);Assert.All(rows,x=>Assert.Equal(new[]{"Code","Id","Name"},x.EnumerateObject().Select(p=>p.Name).OrderBy(x=>x).ToArray()));
        value=await Result("OptionsAsync",f.Db,f.Own.Id,false,Error,default(CancellationToken));using var ordinary=JsonDocument.Parse(JsonSerializer.Serialize(value,value.GetType()));Assert.False(ordinary.RootElement.GetProperty("CanSelectCompany").GetBoolean());Assert.Empty(ordinary.RootElement.GetProperty("Companies").EnumerateArray());Assert.Equal(f.Own.Id,ordinary.RootElement.GetProperty("CompanyId").GetGuid());
    }
    [Theory] [InlineData("valid",true)] [InlineData("revoked",false)] [InlineData("mixed",false)] [InlineData("inactive",false)] [InlineData("deleted-role",false)] [InlineData("deleted-membership",false)]
    public async Task Global_scope_requires_same_live_SuperAdmin_role_to_grant_every_action(string state,bool expected)
    {
        using var f=new Fixture();var role=FinanceOfficerAuthorityTests.Role("SuperAdmin",GranularPermission.HRM_Shift_View,GranularPermission.HRM_Shift_Submit);
        var actor=new User{IsActive=state!="inactive",UserRoles=[new(){Role=role}]};if(state=="deleted-role")role.IsDeleted=true;if(state=="deleted-membership")actor.UserRoles.Single().IsDeleted=true;
        if(state is "revoked" or "mixed")role.GranularPermissions.Remove(role.GranularPermissions.Single(x=>x.Permission==GranularPermission.HRM_Shift_Submit));
        if(state=="mixed")actor.UserRoles.Add(new(){Role=FinanceOfficerAuthorityTests.Role("CompanyAdmin",GranularPermission.HRM_Shift_Submit)});
        f.Db.Add(actor);f.Db.SaveChanges(true);
        var result=await Result("HasGlobalAuthorityAsync",f.Db,actor.Id,new[]{GranularPermission.HRM_Shift_View,GranularPermission.HRM_Shift_Submit},default(CancellationToken));Assert.Equal(expected,(bool)result);
    }
    [Fact]
    public void Resource_context_rejects_selected_or_ordinary_company_mismatches()
    {
        using var f=new Fixture();Assert.Equal(f.Other.Id,(Guid)Call("ForResource",null,true,f.Other.Id,null,Error));
        Assert.Throws<InvalidOperationException>(()=>Call("ForResource",null,true,f.Other.Id,f.Own.Id,Error));
        Assert.Throws<InvalidOperationException>(()=>Call("ForResource",f.Own.Id,false,f.Other.Id,null,Error));
    }
    private static readonly Func<string,Exception> Error=message=>new InvalidOperationException(message);
    private static async Task<Guid> Resolve(Fixture f,Guid? own,bool global,Guid? selected)=>(Guid)await Result("ResolveAsync",f.Db,own,global,selected,Error,default(CancellationToken));
    private static object Call(string name,params object?[] args)
    {
        var type=typeof(EmployeeService).Assembly.GetType("CSM.Infrastructure.Security.CompanyContextResolver");Assert.NotNull(type);
        try{return type.GetMethod(name)!.Invoke(null,args)!;}catch(TargetInvocationException ex){System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException!).Throw();throw;}
    }
    private static async Task<object> Result(string name,params object?[] args){var task=(Task)Call(name,args);await task;return task.GetType().GetProperty("Result")!.GetValue(task)!;}
    private sealed class Fixture:IDisposable
    {
        public ApplicationDbContext Db=new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        public Company Own=new(){Code="OWN",Name="Own",IsActive=true},Other=new(){Code="OTHER",Name="Other",IsActive=true},Inactive=new(){Code="OFF",Name="Inactive",IsActive=false},Deleted=new(){Code="DEL",Name="Deleted",IsActive=true,IsDeleted=true};
        public Fixture(){Db.AddRange(Own,Other,Inactive,Deleted);Db.SaveChanges(true);}public void Dispose()=>Db.Dispose();
    }
}
