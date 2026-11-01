using System.Reflection;
using System.Security.Claims;
using CSM.Domain.Enums;
using CSM.Domain.Constants;
using CSM.Api.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace CSM.Infrastructure.Tests;
public sealed class ProjectChildEndpointTests
{
    [Theory]
    [InlineData(typeof(WbsTasksController), "GetAll", "Projects.View")]
    [InlineData(typeof(WbsTasksController), "Create", "Projects.Manage")]
    [InlineData(typeof(ProjectMilestonesController), "Create", "Projects.Manage")]
    [InlineData(typeof(CSM.API.Controllers.ProjectDelaysController), "Update", "Projects.Manage")]
    [InlineData(typeof(DailySiteDiaryController), "Create", "Projects.DailyProgress.Create")]
    [InlineData(typeof(CSM.API.Controllers.DailyProgressPhotosController), "Download", "Projects.DailyProgress.View")]
    [InlineData(typeof(CSM.API.Controllers.TaskResourceAssignmentsController), "Create", "Projects.Manage")]
    public async Task Actual_endpoint_metadata_rejects_missing_granular_permission(Type controller, string method, string denied)
    {
        var grants = Enum.GetValues<GranularPermission>().Where(p => p.ToString().Replace('_', '.') != denied).ToArray();
        var services = new ServiceCollection(); services.AddLogging();
        using var provider = services.AddGrantedBackendPermissions(AppRoles.CompanyAdmin, grants).BuildServiceProvider();
        var data = controller.GetCustomAttributes(true).OfType<IAuthorizeData>().Concat(controller.GetMethod(method)!.GetCustomAttributes(true).OfType<IAuthorizeData>());
        var policy = await AuthorizationPolicy.CombineAsync(provider.GetRequiredService<IAuthorizationPolicyProvider>(), data);
        var principal = BackendAuthorizationFixture.WithGrantedIdentity(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, AppRoles.CompanyAdmin)], "test")));
        Assert.False((await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(principal, null, policy!)).Succeeded);
    }
}
