using System.Text.Json;
using CSM.Application.Projects.DailyProgress.Dtos;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Entities.Procurement;
using CSM.Domain.Entities.Projects;
using CSM.Domain.Enums;
using CSM.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace CSM.Infrastructure.Tests;
public sealed class DailyUsageLogLinkPostgresTests
{
    [Fact]
    public async Task Explicit_minimal_daily_log_links_usage_without_changing_tasks_or_physical_stock()
    {
        await using var f=await FinanceSettlementPostgresFixture.CreateAsync();await using var db=f.Context();
        var company=new Company{Code="USAGE",Name="Usage company",IsActive=true};
        var site=new ConstructionSite{CompanyId=company.Id,SiteCode="SITE-002",Name="MORMOR",IsActive=true};
        var project=new Project{CompanyId=company.Id,ConstructionSiteId=site.Id,ProjectCode="FENCE",Name="Mor Fence",Status=ProjectStatus.Active};
        var material=new Material{CompanyId=company.Id,MaterialCode="MAT-001",Name="Cement",Category="General",UnitOfMeasure="Bag",IsActive=true};
        var stock=new StockItem{CompanyId=company.Id,ConstructionSiteId=site.Id,MaterialId=material.Id,QuantityOnHand=700};
        var actor=new User{CompanyId=null,Email="usage@example.test",NormalizedEmail="USAGE@EXAMPLE.TEST",IsActive=true,UserRoles=[new(){Role=FinanceOfficerAuthorityTests.Role("SuperAdmin",GranularPermission.Projects_View,GranularPermission.Projects_DailyProgress_View,GranularPermission.Projects_DailyProgress_Create,GranularPermission.Procurement_Inventory_View,GranularPermission.Procurement_Inventory_Adjust)}]};
        db.AddRange(company,site,project,material,stock,actor);await db.SaveChangesAsync();
        var date=DateOnly.FromDateTime(DateTime.UtcNow);
        var request=JsonSerializer.Deserialize<CreateDailyProgressLogRequest>(JsonSerializer.Serialize(new CSM.Web.Models.Projects.CreateDailyProgressRequest{ProjectId=project.Id,LogDate=date}))!;
        var service=new DailyProgressLogService(db);var log=await service.CreateAsync(actor.Id,request);
        Assert.Equal(log.Id,(await service.CreateAsync(actor.Id,request)).Id);
        var older=await service.CreateAsync(actor.Id,request with { LogDate=date.AddDays(-1) });
        Assert.NotEqual(log.Id,older.Id);var logs=await service.GetAllAsync(actor.Id,project.Id);Assert.Equal(2,logs.Count);Assert.Equal(log.Id,logs.First().Id);
        var usage=await new DailyMaterialUsageService(db).CreateAsync(actor.Id,new(){ConstructionSiteId=site.Id,MaterialId=material.Id,DailyProgressLogId=log.Id,Date=date,QuantityIssued=200,QuantityUsed=100,QuantityReturned=50,Unit="Bag",Notes="Usage note"});
        await using var verify=f.Context();var saved=await verify.DailyMaterialUsages.SingleAsync();
        Assert.Equal(log.Id,saved.DailyProgressLogId);Assert.Equal(date,saved.Date);Assert.Equal(50,saved.QuantityVariance);Assert.True(saved.RequiresSupervisorReview);Assert.Equal(usage.Id,saved.Id);
        Assert.Equal(700,(await verify.StockItems.SingleAsync()).QuantityOnHand);Assert.Empty(await verify.StockMovements.ToListAsync());Assert.Equal(2,await verify.DailyProgressLogs.CountAsync());Assert.Empty(await verify.DailyProgressTaskEntries.ToListAsync());Assert.Empty(await verify.WorkTasks.ToListAsync());
    }
}
