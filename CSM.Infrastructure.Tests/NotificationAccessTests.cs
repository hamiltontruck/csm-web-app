using CSM.Application.Common.Exceptions;
using CSM.Application.Notifications.Dtos;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Notifications;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace CSM.Infrastructure.Tests;
public sealed class NotificationAccessTests
{
    [Theory]
    [InlineData("inactive")]
    [InlineData("deleted")]
    [InlineData("no-view")]
    public async Task Current_active_view_grant_is_required_by_read_services(string condition)
    {
        using var f = new Fixture();
        if (condition == "inactive") f.Actor.IsActive = false;
        if (condition == "deleted") f.Actor.IsDeleted = true;
        if (condition == "no-view") f.Db.RoleGranularPermissions.RemoveRange(f.Db.RoleGranularPermissions.Where(x => x.Permission == GranularPermission.Notifications_View));
        f.Db.SaveChanges(true);
        var service = new NotificationService(f.Db);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetAllAsync(f.Actor.Id, null));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetUnreadCountAsync(f.Actor.Id));
    }
    [Fact]
    public async Task View_only_actor_cannot_create_notifications()
    {
        using var f = new Fixture();
        f.Db.RoleGranularPermissions.RemoveRange(f.Db.RoleGranularPermissions.Where(x => x.Permission == GranularPermission.Notifications_Manage)); f.Db.SaveChanges(true);
        var service = new NotificationService(f.Db);
        Assert.Empty(await service.GetAllAsync(f.Actor.Id, null));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CreateAsync(f.Actor.Id, new(f.Actor.Id, "Title", "Message", NotificationSeverity.Info, null, null)));
        Assert.Empty(f.Db.Notifications);
    }
    [Fact]
    public async Task Granted_actor_reads_and_marks_only_own_company_and_recipient_records()
    {
        using var f = new Fixture();
        var service = new NotificationService(f.Db);
        var own = await service.CreateAsync(f.Actor.Id, new(f.Actor.Id, "Own", "Message", NotificationSeverity.Info, null, null));
        var foreign = new Notification { CompanyId = Guid.NewGuid(), RecipientUserId = f.Actor.Id, Title = "Foreign", Message = "Hidden" };
        var otherRecipient = new Notification { CompanyId = f.Actor.CompanyId!.Value, RecipientUserId = Guid.NewGuid(), Title = "Other", Message = "Hidden" };
        f.Db.Notifications.AddRange(foreign, otherRecipient); f.Db.SaveChanges(true);
        Assert.Equal(own.Id, Assert.Single(await service.GetAllAsync(f.Actor.Id, null)).Id);
        Assert.Equal(1, await service.GetUnreadCountAsync(f.Actor.Id));
        Assert.True((await service.MarkReadAsync(f.Actor.Id, own.Id, new(true))).IsRead);
        Assert.Equal(0, await service.GetUnreadCountAsync(f.Actor.Id));
        await Assert.ThrowsAsync<NotificationManagementException>(() => service.MarkReadAsync(f.Actor.Id, foreign.Id, new(true)));
        await Assert.ThrowsAsync<NotificationManagementException>(() => service.MarkReadAsync(f.Actor.Id, otherRecipient.Id, new(true)));
    }
    private sealed class Fixture : IDisposable
    {
        public TestDb Db = new();
        public User Actor = new() { CompanyId = Guid.NewGuid(), IsActive = true, UserRoles = [new() { Role = new Role { Name = "CompanyAdmin", GranularPermissions = [new() { Permission = GranularPermission.Notifications_View }, new() { Permission = GranularPermission.Notifications_Manage }] } }] };
        public Fixture() { Db.Users.Add(Actor); Db.SaveChanges(true); }
        public void Dispose() => Db.Dispose();
    }
    public sealed class TestDb : ApplicationDbContext
    {
        public TestDb() : base(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options) { }
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => base.SaveChangesAsync(true, cancellationToken);
    }
}
