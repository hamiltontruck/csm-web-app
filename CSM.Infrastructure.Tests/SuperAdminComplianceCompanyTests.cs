using System.Text.Json;
using CSM.Application.Common.Exceptions;
using CSM.Application.Compliance.DocumentCategories.Dtos;
using CSM.Application.Compliance.Documents.Dtos;
using CSM.Application.Notifications.Dtos;
using CSM.Domain.Entities.HRM;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Entities.Projects;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace CSM.Infrastructure.Tests;
public sealed class SuperAdminComplianceCompanyTests
{
    [Fact]
    public async Task Global_admin_can_select_company_for_category_and_standalone_document()
    {
        await using var f=await Fixture.CreateAsync();
        var category=JsonSerializer.Deserialize<CreateDocumentCategoryRequest>(JsonSerializer.Serialize(new{CompanyId=f.Other.Id,CategoryCode="DOC",Name="Documents",IsActive=true}))!;
        var result=await new DocumentCategoryService(f.Db).CreateAsync(f.Admin.Id,category);
        var doc=await new DocumentService(f.Db).CreateAsync(f.Admin.Id,f.Document(f.Other.Id));
        await using var check=f.Pg.Context();Assert.Equal(f.Other.Id,(await check.DocumentCategories.SingleAsync(x=>x.Id==result.Id)).CompanyId);Assert.Equal(f.Other.Id,(await check.Documents.SingleAsync(x=>x.Id==doc.Id)).CompanyId);Assert.Null((await check.Users.SingleAsync(x=>x.Id==f.Admin.Id)).CompanyId);
    }
    [Fact]
    public async Task Document_anchor_supplies_company_and_rejects_conflicting_selection()
    {
        await using var f=await Fixture.CreateAsync();var request=f.Document(null);request.ConstructionSiteId=f.Site.Id;request.ProjectId=f.Project.Id;request.EmployeeId=f.Employee.Id;
        var created=await new DocumentService(f.Db).CreateAsync(f.Admin.Id,request);Assert.Equal(f.Own.Id,created.CompanyId);
        request=f.Document(f.Other.Id);request.ConstructionSiteId=f.Site.Id;
        await Assert.ThrowsAsync<DocumentManagementException>(()=>new DocumentService(f.Db).CreateAsync(f.Admin.Id,request));Assert.Single(await f.Db.Documents.ToListAsync());
    }
    [Fact]
    public async Task Ordinary_company_user_cannot_inject_foreign_company()
    {
        await using var f=await Fixture.CreateAsync();var role=f.Recipient.UserRoles.Single().Role;
        f.Db.RoleGranularPermissions.AddRange(new RoleGranularPermission{RoleId=role.Id,Permission=GranularPermission.Compliance_View}, new RoleGranularPermission{RoleId=role.Id,Permission=GranularPermission.Compliance_Manage});
        var actor=new User{CompanyId=f.Own.Id,Email="ordinary@example.test",NormalizedEmail="ORDINARY@EXAMPLE.TEST",IsActive=true,UserRoles=[new(){Role=role}]};f.Db.Add(actor);await f.Db.SaveChangesAsync();
        var category=JsonSerializer.Deserialize<CreateDocumentCategoryRequest>(JsonSerializer.Serialize(new{CompanyId=f.Other.Id,CategoryCode="DOC",Name="Documents"}))!;
        await Assert.ThrowsAsync<DocumentCategoryManagementException>(()=>new DocumentCategoryService(f.Db).CreateAsync(actor.Id,category));
        await Assert.ThrowsAsync<DocumentManagementException>(()=>new DocumentService(f.Db).CreateAsync(actor.Id,f.Document(f.Other.Id)));Assert.Empty(await f.Db.Documents.ToListAsync());Assert.Empty(await f.Db.DocumentCategories.ToListAsync());
    }
    [Fact]
    public async Task Global_notification_uses_recipient_company_and_keeps_recipient_only_visibility()
    {
        await using var f=await Fixture.CreateAsync();var service=new NotificationService(f.Db);
        var result=await service.CreateAsync(f.Admin.Id,new(f.Recipient.Id,"Recipient message","Private recipient message",NotificationSeverity.Info,null,null));
        await using var check=f.Pg.Context();var row=await check.Notifications.SingleAsync(x=>x.Id==result.Id);Assert.Equal(f.Other.Id,row.CompanyId);Assert.Equal(f.Recipient.Id,row.RecipientUserId);
        Assert.Empty(await service.GetAllAsync(f.Admin.Id,null));Assert.Single(await service.GetAllAsync(f.Recipient.Id,null));
    }
    [Fact]
    public async Task Companyless_recipient_can_read_own_explicit_company_notifications_without_reading_others()
    {
        await using var f=await Fixture.CreateAsync();var service=new NotificationService(f.Db);
        CreateNotificationRequest Request(Guid company,string title)=>JsonSerializer.Deserialize<CreateNotificationRequest>(JsonSerializer.Serialize(new{CompanyId=company,RecipientUserId=f.Admin.Id,Title=title,Message="Private",Severity=1}))!;
        var a=await service.CreateAsync(f.Admin.Id,Request(f.Own.Id,"Mine A"));await service.CreateAsync(f.Admin.Id,Request(f.Other.Id,"Mine B"));
        await service.CreateAsync(f.Admin.Id,new(f.Recipient.Id,"Other person","Other",NotificationSeverity.Info,null,null));
        var mine=await service.GetAllAsync(f.Admin.Id,null);Assert.Equal(2,mine.Count);Assert.DoesNotContain(mine,x=>x.Title=="Other person");Assert.Equal(2,await service.GetUnreadCountAsync(f.Admin.Id));
        await Assert.ThrowsAsync<NotificationManagementException>(()=>service.MarkReadAsync(f.Recipient.Id,a.Id,new(true)));
        await service.MarkReadAsync(f.Admin.Id, a.Id, new(true));
        Assert.Equal(1, await service.GetUnreadCountAsync(f.Admin.Id));
        await using var check = f.Pg.Context();
        Assert.True((await check.Notifications.SingleAsync(x => x.Id == a.Id)).IsRead);
    }
    [Fact]
    public async Task Global_safety_incident_derives_company_from_site()
    {
        await using var f = await Fixture.CreateAsync();
        var request = new CSM.Application.Compliance.SafetyIncidents.Dtos.CreateSafetyIncidentRequest
        {
            ConstructionSiteId = f.Site.Id, IncidentNumber = "INC-1", OccurredAtUtc = DateTime.UtcNow,
            LocationDescription = "Site", Description = "Test incident", Severity = SafetyIncidentSeverity.Minor
        };
        var created = await new SafetyIncidentService(f.Db).CreateAsync(f.Admin.Id, request);
        Assert.Equal(f.Own.Id, (await f.Db.SafetyIncidents.SingleAsync(x => x.Id == created.Id)).CompanyId);
    }
    [Fact]
    public async Task Unanchored_global_creates_require_selection_and_notification_selection_must_match_recipient()
    {
        await using var f = await Fixture.CreateAsync();
        await Assert.ThrowsAsync<DocumentManagementException>(() => new DocumentService(f.Db).CreateAsync(f.Admin.Id, f.Document(null)));
        await Assert.ThrowsAsync<DocumentCategoryManagementException>(() => new DocumentCategoryService(f.Db).CreateAsync(
            f.Admin.Id, new() { CategoryCode = "DOC", Name = "Documents" }));
        await Assert.ThrowsAsync<NotificationManagementException>(() => new NotificationService(f.Db).CreateAsync(
            f.Admin.Id, new(f.Recipient.Id, "Message", "Private", NotificationSeverity.Info, null, null, f.Own.Id)));
        Assert.Empty(await f.Db.Documents.ToListAsync());
        Assert.Empty(await f.Db.DocumentCategories.ToListAsync());
        Assert.Empty(await f.Db.Notifications.ToListAsync());
    }

    private sealed class Fixture:IAsyncDisposable
    {
        public FinanceSettlementPostgresFixture Pg=null!;public ApplicationDbContext Db=null!;
        public Company Own=new(){Code="OWN",Name="Own",IsActive=true},Other=new(){Code="OTHER",Name="Other",IsActive=true};public ConstructionSite Site=null!;public Project Project=null!;public Employee Employee=null!;public User Admin=null!,Recipient=null!;
        public static async Task<Fixture> CreateAsync()
        {
            var f=new Fixture{Pg=await FinanceSettlementPostgresFixture.CreateAsync()};f.Db=f.Pg.Context();
            f.Site=new(){CompanyId=f.Own.Id,SiteCode="SITE",Name="Own site",IsActive=true};f.Project=new(){CompanyId=f.Own.Id,ConstructionSiteId=f.Site.Id,ProjectCode="PROJ",Name="Project",Status=ProjectStatus.Active};
            f.Employee=new(){CompanyId=f.Own.Id,EmployeeNumber="EMP",FirstName="Own",LastName="Employee",HireDate=new(2026,1,1),Status=EmployeeStatus.Active};
            f.Admin=new(){CompanyId=null,Email="global@example.test",NormalizedEmail="GLOBAL@EXAMPLE.TEST",IsActive=true,UserRoles=[new(){Role=FinanceOfficerAuthorityTests.Role("SuperAdmin",GranularPermission.Compliance_View,GranularPermission.Compliance_Manage,GranularPermission.Notifications_View,GranularPermission.Notifications_Manage)}]};
            f.Recipient=new(){CompanyId=f.Other.Id,Email="recipient@example.test",NormalizedEmail="RECIPIENT@EXAMPLE.TEST",IsActive=true,UserRoles=[new(){Role=FinanceOfficerAuthorityTests.Role("CompanyAdmin",GranularPermission.Notifications_View)}]};
            f.Db.AddRange(f.Own,f.Other,f.Site,f.Project,f.Employee,f.Admin,f.Recipient);await f.Db.SaveChangesAsync();return f;
        }
        public CreateDocumentRequest Document(Guid? company)=>JsonSerializer.Deserialize<CreateDocumentRequest>(JsonSerializer.Serialize(new{CompanyId=company,DocumentNumber="DOC-"+Guid.NewGuid().ToString("N")[..8],Name="Document",DocumentType=1,FileName="document.pdf",StoragePath="",FileSizeBytes=10}))!;
        public async ValueTask DisposeAsync(){await Db.DisposeAsync();await Pg.DisposeAsync();}
    }
}
