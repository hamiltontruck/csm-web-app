using System.Reflection;
using CSM.Application.Common.Exceptions;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Services;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class FinanceRoleOptionsTests
{
    [Theory]
    [InlineData("CompanyAdmin", "Users_Create", true)]
    [InlineData("CompanyAdmin", "Users_Edit", true)]
    [InlineData("SuperAdmin", "Users_Create", true)]
    [InlineData("CompanyAdmin", "Users_View", false)]
    [InlineData("FinanceOfficer", "Users_Create", false)]
    [InlineData("inactive", "Users_Create", false)]
    public async Task Options_require_live_user_management_authority_and_return_only_actual_assignable_roles(string role, string grant, bool allowed)
    {
        using var db = FinanceOfficerAuthorityTests.Database();
        var actor = new User { Email = "manager@example.test", IsActive = role != "inactive", UserRoles = [new() { Role = FinanceOfficerAuthorityTests.Role(role == "inactive" ? "CompanyAdmin" : role, Enum.Parse<GranularPermission>(grant)) }] };
        db.Users.Add(actor);
        db.Roles.Add(FinanceOfficerAuthorityTests.Role("FinanceOfficer", GranularPermission.Sites_View));
        db.Roles.Add(new Role { Name = "DeletedRole", NormalizedName = "DELETEDROLE", IsDeleted = true });
        await db.SaveChangesAsync();
        var type = typeof(UserService).Assembly.GetType("CSM.Infrastructure.Services.UserRoleOptionsService");
        Assert.NotNull(type);
        var service = Activator.CreateInstance(type, db)!;
        async Task<IReadOnlyCollection<string>> Read() => await (Task<IReadOnlyCollection<string>>)type.GetMethod("GetAssignableRolesAsync")!.Invoke(service, [actor.Id, CancellationToken.None])!;
        if (!allowed) { await Assert.ThrowsAsync<UserManagementException>(Read); return; }
        var names = await Read();
        Assert.Contains("FinanceOfficer", names);
        Assert.DoesNotContain("SuperAdmin", names);
        Assert.DoesNotContain("DeletedRole", names);
        Assert.DoesNotContain("EquipmentManager", names);
    }
}
