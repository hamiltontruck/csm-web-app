using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Bunit;
using CSM.Web.Components.Pages;
using CSM.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;
public sealed class RolePermissionUiTests
{
    [Theory]
    [InlineData("CompanyAdmin", true, false)]
    [InlineData("SuperAdmin", false, false)]
    [InlineData("SuperAdmin", true, true)]
    public void Shared_permission_controls_require_superadmin_and_write_grant(string role, bool edit, bool allowed)
    {
        using var context = new TestContext();
        var auth = DispatchProxy.Create<IAuthService, Auth>();
        context.Services.AddSingleton(auth);
        context.Services.AddSingleton(new RoleWebService(new HttpClient(new Handler(false)) { BaseAddress = new Uri("https://api.test/") }, auth));
        WorkspaceTestAccess.Add(context, [role], edit ? ["Users_View", "Users_Edit"] : ["Users_View"]);
        var page = context.RenderComponent<Roles>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("input[type=checkbox]")));
        Assert.Equal(!allowed, page.Find("input").HasAttribute("disabled"));
        Assert.Equal(allowed, page.FindAll("button").Any(b => b.TextContent.Contains("Save Changes")));
    }

    [Fact]
    public async Task Role_client_reads_every_permission_name_returned_by_API()
    {
        var auth = DispatchProxy.Create<IAuthService, Auth>();
        var service = new RoleWebService(new HttpClient(new Handler(true)) { BaseAddress = new Uri("https://api.test/") }, auth);
        var permissions = await service.GetPermissionsAsync(Guid.NewGuid());
        Assert.Contains(permissions, p => p.Permission.ToString() == "HRM_Attendance_Approve");
    }

    public class Auth : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) => Task.CompletedTask;
    }
    private sealed class Handler(bool all) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            object data = request.RequestUri!.AbsolutePath.EndsWith("/permissions")
                ? (all ? Enum.GetNames<CSM.Domain.Enums.GranularPermission>() : new[] { "Users_View" }).Select(p => new { permission = p, granted = true }).ToArray()
                : new[] { new { id = Guid.Parse("6c066e8b-a418-4ac3-b442-5656a668c779"), name = "Supervisor" } };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(data) });
        }
    }
}
