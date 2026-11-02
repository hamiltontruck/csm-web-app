using CSM.Application.Common.Exceptions;
using CSM.Application.Notifications;
using CSM.Application.Notifications.Dtos;
using CSM.Application.Procurement.MaterialRequests.Dtos;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Entities.Procurement;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class MaterialRequestNotificationTests
{
    [Fact]
    public async Task Site_manager_creates_request_and_own_workflow_notification_without_notification_management()
    {
        using var f = new Fixture();
        var result = await f.Service.CreateAsync(f.Actor.Id, f.Input());
        Assert.Equal(MaterialRequestStatus.Draft, result.Status);
        var notification = Assert.Single(f.Db.Notifications);
        Assert.Equal(f.Actor.Id, notification.RecipientUserId);
        Assert.Equal(f.CompanyId, notification.CompanyId);
        Assert.Equal(result.Id, notification.SourceEntityId);
        Assert.Equal("MaterialRequest", notification.SourceEntityType);
        Assert.Equal("Material Request Created", notification.Title);
        Assert.Equal(1, f.Db.Saves);
        Assert.False(f.Db.RoleGranularPermissions.Any(x => x.Permission == GranularPermission.Notifications_Manage));
    }

    [Fact]
    public async Task Submit_commits_status_and_notifies_only_active_same_company_admins_in_one_save()
    {
        using var f = new Fixture(); var request = f.SeedRequest(MaterialRequestStatus.Draft);
        var result = await f.Service.SubmitAsync(f.Actor.Id, request.Id);
        Assert.Equal(MaterialRequestStatus.Submitted, result.Status);
        var notification = Assert.Single(f.Db.Notifications);
        Assert.Equal(f.Admin.Id, notification.RecipientUserId);
        Assert.Equal(f.CompanyId, notification.CompanyId);
        Assert.Equal(request.Id, notification.SourceEntityId);
        Assert.Equal("Material Request Submitted", notification.Title);
        Assert.Equal(1, f.Db.Saves);
    }

    [Fact]
    public async Task Approver_without_manual_notification_grant_can_approve_and_notify_requester()
    {
        using var f = new Fixture(); var request = f.SeedRequest(MaterialRequestStatus.Submitted);
        var result = await f.Service.ApproveAsync(f.Admin.Id, request.Id, new ApproveMaterialRequestRequest {
            Lines = [new() { MaterialRequestLineId = request.Lines.Single().Id, ApprovedQuantity = 1 }] });
        Assert.Equal(MaterialRequestStatus.Approved, result.Status);
        var notification = Assert.Single(f.Db.Notifications);
        Assert.Equal(f.Actor.Id, notification.RecipientUserId);
        Assert.Equal(f.CompanyId, notification.CompanyId);
        Assert.Equal("Material Request Approved", notification.Title);
        Assert.Equal(1, f.Db.Saves);
    }

    [Fact]
    public async Task Operational_workflow_does_not_grant_manual_notification_creation()
    {
        using var f = new Fixture();
        await f.Service.CreateAsync(f.Actor.Id, f.Input());
        var service = new NotificationService(f.Db);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CreateAsync(f.Actor.Id,
            new CreateNotificationRequest(f.Admin.Id, "Arbitrary", "Forbidden", NotificationSeverity.Info, null, null)));
        Assert.Single(f.Db.Notifications);
    }

    [Theory]
    [InlineData("create")] [InlineData("submit")] [InlineData("approve")]
    public async Task Notification_persistence_failure_does_not_commit_a_request_or_status_alone(string action)
    {
        using var f = new Fixture();
        MaterialRequest? existing = action == "create" ? null : f.SeedRequest(
            action == "submit" ? MaterialRequestStatus.Draft : MaterialRequestStatus.Submitted);
        f.Db.FailNotificationSave = true;
        await Assert.ThrowsAsync<DbUpdateException>(async () => {
            if (action == "create") await f.Service.CreateAsync(f.Actor.Id, f.Input());
            else if (action == "submit") await f.Service.SubmitAsync(f.Actor.Id, existing!.Id);
            else await f.Service.ApproveAsync(f.Admin.Id, existing!.Id, new ApproveMaterialRequestRequest {
                Lines = [new() { MaterialRequestLineId = existing.Lines.Single().Id, ApprovedQuantity = 1 }] });
        });
        using var fresh = new TestDb(f.Options);
        Assert.Empty(fresh.Notifications);
        if (action == "create") Assert.Empty(fresh.MaterialRequests);
        else Assert.Equal(action == "submit" ? MaterialRequestStatus.Draft : MaterialRequestStatus.Submitted,
            Assert.Single(fresh.MaterialRequests).Status);
    }

    [Theory]
    [InlineData("inactive")] [InlineData("foreign-company")] [InlineData("wrong-role")]
    public async Task Unauthorized_actor_cannot_create_request_or_workflow_notification(string reason)
    {
        using var f = new Fixture();
        if (reason == "inactive") f.Actor.IsActive = false;
        if (reason == "foreign-company") f.Actor.CompanyId = Guid.NewGuid();
        if (reason == "wrong-role") f.Actor.UserRoles.Single().Role.Name = "Storekeeper";
        f.Db.SaveChanges(true);
        await Assert.ThrowsAsync<MaterialRequestManagementException>(() => f.Service.CreateAsync(f.Actor.Id, f.Input()));
        using var fresh = new TestDb(f.Options);
        Assert.Empty(fresh.MaterialRequests); Assert.Empty(fresh.Notifications);
    }

    private sealed class Fixture : IDisposable
    {
        public readonly Guid CompanyId = Guid.NewGuid();
        public readonly DbContextOptions<ApplicationDbContext> Options;
        public readonly TestDb Db;
        public readonly User Actor, Admin;
        public readonly ConstructionSite Site;
        public readonly Material Material;
        public readonly ServiceProvider Provider;
        public MaterialRequestService Service => Provider.GetRequiredService<MaterialRequestService>();
        public Fixture()
        {
            Options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
            Db = new TestDb(Options);
            Actor = User("SiteManager", CompanyId);
            Admin = User("CompanyAdmin", CompanyId);
            Db.Users.AddRange(Actor, Admin, User("CompanyAdmin", Guid.NewGuid()), User("CompanyAdmin", CompanyId, active: false),
                User("CompanyAdmin", CompanyId, deleted: true), User("CompanyAdmin", CompanyId, deletedRole: true));
            Site = new() { CompanyId = CompanyId, Name = "Test Site", SiteCode = "SITE-001", IsActive = true };
            Material = new() { CompanyId = CompanyId, Name = "Cement", MaterialCode = "MAT-001", UnitOfMeasure = "Bag", IsActive = true };
            Db.ConstructionSites.Add(Site); Db.Materials.Add(Material); Db.SaveChanges(true);
            Provider = new ServiceCollection().AddSingleton<ApplicationDbContext>(Db)
                .AddSingleton<INotificationService>(new NotificationService(Db)).AddSingleton<NotificationHelper>()
                .AddTransient<MaterialRequestService>().BuildServiceProvider();
        }
        private static User User(string role, Guid company, bool active = true, bool deleted = false, bool deletedRole = false) => new() {
            CompanyId = company, IsActive = active, IsDeleted = deleted, FirstName = "Test", LastName = role,
            UserRoles = [new() { Role = new Role { Name = role, IsDeleted = deletedRole, GranularPermissions = [
                new() { Permission = GranularPermission.Notifications_View },
                new() { Permission = GranularPermission.Procurement_MaterialRequest_View },
                new() { Permission = GranularPermission.Procurement_MaterialRequest_Submit },
                new() { Permission = GranularPermission.Procurement_MaterialRequest_Approve }] } }] };
        public CreateMaterialRequestRequest Input() => new() { ConstructionSiteId = Site.Id,
            RequestNumber = "MR-SM-TEST-001", RequestDate = new(2026,11,1), Purpose = "Permission test",
            Lines = [new() { MaterialId = Material.Id, RequestedQuantity = 1 }] };
        public MaterialRequest SeedRequest(MaterialRequestStatus status)
        {
            var request = new MaterialRequest { CompanyId = CompanyId, ConstructionSiteId = Site.Id, RequestedBy = Actor.Id,
                RequestNumber = "MR-EXISTING", RequestDate = new(2026,11,1), Status = status,
                Lines = [new() { CompanyId = CompanyId, MaterialId = Material.Id, RequestedQuantity = 1 }] };
            Db.MaterialRequests.Add(request); Db.SaveChanges(true); Db.Saves = 0; return request;
        }
        public void Dispose() { Provider.Dispose(); Db.Dispose(); }
    }
    public sealed class TestDb(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
    {
        public int Saves; public bool FailNotificationSave;
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            Saves++;
            if (FailNotificationSave && ChangeTracker.Entries<CSM.Domain.Entities.Notifications.Notification>()
                .Any(x => x.State == EntityState.Added)) throw new DbUpdateException("Synthetic notification save failure");
            return base.SaveChangesAsync(true, cancellationToken);
        }
    }
}
