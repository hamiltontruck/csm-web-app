using System.Security.Claims;
using CSM.Api.Controllers;
using CSM.Application.Common.Security;
using CSM.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class EquipmentAuthorizationTests
{
    [Theory]
    [InlineData(AppRoles.SuperAdmin, true)]
    [InlineData(AppRoles.CompanyAdmin, true)]
    [InlineData(AppRoles.SiteManager, true)]
    [InlineData("UnrelatedRole", false)]
    [InlineData(AppRoles.Supervisor, false)]
    public async Task Controller_admits_equipment_service_roles_and_rejects_unrelated_role(string role, bool expected)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options =>
            options.AddPolicy(AppPolicies.ProcurementManagement,
                policy => policy.RequireRole(AppPolicies.ProcurementManagementRoles)));
        using var provider = services.AddGrantedBackendPermissions(role).BuildServiceProvider();
        var policy = await AuthorizationPolicy.CombineAsync(
            provider.GetRequiredService<IAuthorizationPolicyProvider>(),
            typeof(EquipmentController).GetCustomAttributes(true).OfType<IAuthorizeData>());
        var actor = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "test"));
        var result = await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(BackendAuthorizationFixture.WithGrantedIdentity(actor), null, policy!);
        Assert.Equal(expected, result.Succeeded);
    }
}
