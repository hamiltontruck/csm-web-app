using System.Reflection;
using System.Security.Claims;
using CSM.Api.Controllers;
using CSM.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class AuditBackupEndpointAuthorizationTests
{
    [Theory]
    [InlineData("SuperAdmin", true, true)]
    [InlineData("CompanyAdmin", true, true)]
    [InlineData("Administrator", true, true)]
    [InlineData("Supervisor", true, false)]
    [InlineData("CompanyAdmin", false, false)]
    [InlineData("SuperAdmin", false, false)]
    public async Task Audit_combined_metadata_enforces_real_database_permission(string role, bool grant, bool expected)
    {
        Assert.Equal(expected, await Allowed(typeof(AuditController), role, grant));
    }

    [Theory]
    [InlineData("SuperAdmin", true)]
    [InlineData("CompanyAdmin", false)]
    [InlineData("Administrator", false)]
    [InlineData("Supervisor", false)]
    public async Task Backup_combined_metadata_requires_global_authority(string role, bool expected)
    {
        Assert.Equal(expected, await Allowed(typeof(BackupController), role, true));
    }

    private static async Task<bool> Allowed(Type controller, string role, bool grant)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddGrantedBackendPermissions(role, grant ? [GranularPermission.Users_View] : []);
        services.AddAuthorization(options =>
        {
            options.AddPolicy("UserManagement", p => p.RequireRole("SuperAdmin", "CompanyAdmin", "Administrator"));
            options.AddPolicy("CompanyManagement", p => p.RequireRole("SuperAdmin", "CompanyAdmin", "Administrator"));
        });
        await using var provider = services.BuildServiceProvider();
        var policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();
        var policy = await AuthorizationPolicy.CombineAsync(policyProvider, controller.GetCustomAttributes<AuthorizeAttribute>());
        var actor = BackendAuthorizationFixture.WithGrantedIdentity(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "test")));
        return (await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(actor, null, policy!)).Succeeded;
    }
}
