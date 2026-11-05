using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using Bunit;
using CSM.Domain.Enums;
using CSM.Web.Components.Pages;
using CSM.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class AccountantReadOnlyUiTests
{
    private static readonly Guid SiteId = Guid.Parse("ae408187-2e4a-41b2-85d9-c4ae6f9e4f66");

    [Fact]
    public void Accountant_can_read_active_assignment_without_release_control()
    {
        using var context = new TestContext();
        WorkspaceTestAccess.Add(context, ["Accountant"], Enum.GetNames<GranularPermission>());
        context.Services.AddSingleton(DispatchProxy.Create<IEquipmentService, EquipmentAssignmentFeedbackTests.Api>());
        var page = context.RenderComponent<EquipmentAssignments>(p => p.Add(x => x.SiteId, SiteId));
        page.WaitForAssertion(() => Assert.Contains("Excavator", page.Markup));
        Assert.DoesNotContain(page.FindAll("button"), b => b.TextContent.Trim() == "Release");
        Assert.DoesNotContain("New Assignment", page.Markup);
    }

    [Fact]
    public void Accountant_can_read_open_downtime_without_close_control()
    {
        using var context = new TestContext();
        WorkspaceTestAccess.Add(context, ["Accountant"], Enum.GetNames<GranularPermission>());
        context.Services.AddSingleton(DispatchProxy.Create<IEquipmentService, EquipmentDowntimeFeedbackTests.Api>());
        context.Services.AddSingleton(DispatchProxy.Create<IProjectService, EquipmentDowntimeFeedbackTests.Sites>());
        var page = context.RenderComponent<EquipmentDowntime>(p => p.Add(x => x.SiteId, SiteId));
        page.WaitForAssertion(() => Assert.Contains("DT-001", page.Markup));
        Assert.DoesNotContain(page.FindAll("button"), b => b.TextContent.Trim() == "Close");
        Assert.DoesNotContain("New Downtime", page.Markup);
    }

    [Fact]
    public void Matrix_disables_unsupported_Accountant_write_grant()
    {
        using var context = new TestContext();
        WorkspaceTestAccess.Add(context, ["SuperAdmin"], ["Users_View", "Users_Edit"]);
        var auth = DispatchProxy.Create<IAuthService, Auth>();
        context.Services.AddSingleton(new RoleWebService(new HttpClient(new Handler(true)) { BaseAddress = new Uri("https://api.test/") }, auth));
        var page = context.RenderComponent<Roles>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("input[type=checkbox]")));
        Assert.True(page.Find("input").HasAttribute("disabled"));
        Assert.False(page.Find("input").HasAttribute("checked"));
    }

    [Fact]
    public void Read_only_user_list_hides_activation_deletion_and_editing()
    {
        using var context = new TestContext();
        WorkspaceTestAccess.Add(context, ["CompanyAdmin"], ["Users_View"]);
        var auth = DispatchProxy.Create<IAuthService, Auth>();
        context.Services.AddSingleton(new UserWebService(new HttpClient(new Handler(false)) { BaseAddress = new Uri("https://api.test/") }, auth));
        var page = context.RenderComponent<Users>();
        page.WaitForAssertion(() => Assert.Contains("own@example.test", page.Markup));
        Assert.DoesNotContain(page.FindAll("button"), b => new[] { "Edit", "Disable", "Enable", "Delete", "+ New User" }.Contains(b.TextContent.Trim()));
    }

    [Theory]
    [InlineData("Draft")] [InlineData("Submitted")]
    public void Accountant_material_request_details_ignore_old_submit_and_approve_grants(string status)
    {
        using var context = new TestContext();
        WorkspaceTestAccess.Add(context, ["Accountant"], Enum.GetNames<GranularPermission>());
        var api = DispatchProxy.Create<IProcurementService, MaterialRequestWorkflowTests.ProcurementProxy>();
        var data = (MaterialRequestWorkflowTests.ProcurementProxy)(object)api;
        data.Request.Status = status;
        context.Services.AddSingleton(api);
        context.Services.AddSingleton(DispatchProxy.Create<IAuthService, MaterialRequestWorkflowTests.AuthProxy>());
        var page = context.RenderComponent<MaterialRequestDetails>(p => p.Add(x => x.Id, data.Request.Id));
        Assert.Contains("MR-2026-001", page.Markup);
        Assert.Empty(page.FindAll("#submit-material-request, #approve-material-request, input[type=number]"));
    }

    public class Auth : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            method!.Name == nameof(IAuthService.GetTokenAsync) ? Task.FromResult<string?>("test-token") : Task.CompletedTask;
    }

    private sealed class Handler(bool matrix) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            object data = matrix
                ? (request.RequestUri!.AbsolutePath.EndsWith("/permissions")
                    ? new[] { new { permission = "Users_Delete", granted = false, canGrant = false } } as object
                    : new[] { new { id = Guid.Parse("6c066e8b-a418-4ac3-b442-5656a668c779"), name = "Accountant" } })
                : new[] { new { id = Guid.NewGuid(), firstName = "Own", lastName = "User", fullName = "Own User", email = "own@example.test", isActive = true, roles = new[] { "Employee" }, siteAccesses = Array.Empty<object>() },
                          new { id = Guid.NewGuid(), firstName = "Inactive", lastName = "User", fullName = "Inactive User", email = "inactive@example.test", isActive = false, roles = new[] { "Employee" }, siteAccesses = Array.Empty<object>() } };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(data) });
        }
    }
}
