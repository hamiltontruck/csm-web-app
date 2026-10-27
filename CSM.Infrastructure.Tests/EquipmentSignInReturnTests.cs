using System.Net;
using System.Reflection;
using Bunit;
using CSM.Web.Components.Pages;
using CSM.Web.Models.Auth;
using CSM.Web.Models.Equipment;
using CSM.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class EquipmentSignInReturnTests
{
    [Fact]
    public void Equipment_create_sign_in_link_preserves_the_destination()
    {
        using var context = new TestContext();
        context.Services.AddSingleton(DispatchProxy.Create<IEquipmentService, DeniedEquipment>());
        var page = context.RenderComponent<EquipmentCreate>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("#equipment-create-sign-in")));
        Assert.Equal("/login?returnUrl=%2Fequipment%2Fcreate",
            page.Find("#equipment-create-sign-in").GetAttribute("href"));
    }

    [Theory]
    [InlineData("/equipment/create", "/equipment/create")]
    [InlineData(null, "/dashboard")]
    [InlineData("/hr/leave", "/hr/leave")]
    [InlineData("/hr/leave/create", "/hr/leave/create")]
    [InlineData("/hr/leave/external", "/dashboard")]
    [InlineData("/hr/shifts", "/hr/shifts")]
    [InlineData("/hr/shifts/create", "/hr/shifts/create")]
    [InlineData("/hr/shifts/edit/11111111-1111-1111-1111-111111111111", "/hr/shifts/edit/11111111-1111-1111-1111-111111111111")]
    [InlineData("/hr/shifts/edit/invalid", "/dashboard")]
    [InlineData("/hr/site-assignments", "/hr/site-assignments")]
    [InlineData("/hr/site-assignments/create", "/hr/site-assignments/create")]
    [InlineData("/hr/site-assignments/edit/11111111-1111-1111-1111-111111111111", "/hr/site-assignments/edit/11111111-1111-1111-1111-111111111111")]
    [InlineData("/hr/site-assignments/edit/invalid", "/dashboard")]
    [InlineData("/hr/attendance", "/hr/attendance")]
    [InlineData("/hr/attendance/check-in", "/hr/attendance/check-in")]
    [InlineData("/hr/attendance/check-out/11111111-1111-1111-1111-111111111111", "/hr/attendance/check-out/11111111-1111-1111-1111-111111111111")]
    [InlineData("/hr/attendance/override/11111111-1111-1111-1111-111111111111", "/hr/attendance/override/11111111-1111-1111-1111-111111111111")]
    [InlineData("/hr/attendance/check-out/invalid", "/dashboard")]
    [InlineData("/hr/attendance/override/invalid", "/dashboard")]
    [InlineData("/hr/employees/company", "/hr/employees/company")]
    [InlineData("/hr/employees/site", "/hr/employees/site")]
    [InlineData("/hr/employees/create", "/hr/employees/create")]
    [InlineData("/hr/employees", "/hr/employees")]
    [InlineData("/hr/employees/site/ae408187-2e4a-41b2-85d9-c4ae6f9e4f66", "/hr/employees/site/ae408187-2e4a-41b2-85d9-c4ae6f9e4f66")]
    [InlineData("/hr/employees/site/invalid", "/dashboard")]
    [InlineData("/hr", "/hr")]
    [InlineData("/equipment/timesheet/create/ae408187-2e4a-41b2-85d9-c4ae6f9e4f66", "/equipment/timesheet/create/ae408187-2e4a-41b2-85d9-c4ae6f9e4f66")]
    [InlineData("/equipment/timesheet/create/invalid", "/dashboard")]
    [InlineData("/equipment/timesheet/site/ae408187-2e4a-41b2-85d9-c4ae6f9e4f66", "/equipment/timesheet/site/ae408187-2e4a-41b2-85d9-c4ae6f9e4f66")]
    [InlineData("/equipment/timesheet/site/invalid", "/dashboard")]
    [InlineData("/equipment/timesheet/report/ae408187-2e4a-41b2-85d9-c4ae6f9e4f66", "/equipment/timesheet/report/ae408187-2e4a-41b2-85d9-c4ae6f9e4f66")]
    [InlineData("/equipment/timesheet/report/invalid", "/dashboard")]
    [InlineData("/equipment/timesheet/edit/ae408187-2e4a-41b2-85d9-c4ae6f9e4f66", "/equipment/timesheet/edit/ae408187-2e4a-41b2-85d9-c4ae6f9e4f66")]
    [InlineData("/equipment/timesheet/edit/invalid", "/dashboard")]
    [InlineData("/equipment/hourmeter/create/ae408187-2e4a-41b2-85d9-c4ae6f9e4f66", "/equipment/hourmeter/create/ae408187-2e4a-41b2-85d9-c4ae6f9e4f66")]
    [InlineData("/equipment/hourmeter/create/invalid", "/dashboard")]
    [InlineData("/equipment/hourmeter/site/ae408187-2e4a-41b2-85d9-c4ae6f9e4f66", "/equipment/hourmeter/site/ae408187-2e4a-41b2-85d9-c4ae6f9e4f66")]
    [InlineData("https://example.test/equipment/create", "/dashboard")]
    [InlineData("//example.test/equipment/create", "/dashboard")]
    [InlineData("/equipment/list", "/dashboard")]
    [InlineData("/equipment/fuel/create/ae408187-2e4a-41b2-85d9-c4ae6f9e4f66", "/equipment/fuel/create/ae408187-2e4a-41b2-85d9-c4ae6f9e4f66")]
    [InlineData("/equipment/fuel/create/invalid", "/dashboard")]
    [InlineData("/equipment/fuel/edit/ae408187-2e4a-41b2-85d9-c4ae6f9e4f66/11111111-1111-1111-1111-111111111111", "/equipment/fuel/edit/ae408187-2e4a-41b2-85d9-c4ae6f9e4f66/11111111-1111-1111-1111-111111111111")]
    [InlineData("/equipment/fuel/edit/ae408187-2e4a-41b2-85d9-c4ae6f9e4f66/invalid", "/dashboard")]
    [InlineData("/equipment/maintenance/create/ae408187-2e4a-41b2-85d9-c4ae6f9e4f66", "/equipment/maintenance/create/ae408187-2e4a-41b2-85d9-c4ae6f9e4f66")]
    [InlineData("/equipment/maintenance/create/invalid", "/dashboard")]
    [InlineData("/equipment/downtime/create/ae408187-2e4a-41b2-85d9-c4ae6f9e4f66", "/equipment/downtime/create/ae408187-2e4a-41b2-85d9-c4ae6f9e4f66")]
    [InlineData("/equipment/downtime/create/invalid", "/dashboard")]
    [InlineData("/equipment/assignments/create/ae408187-2e4a-41b2-85d9-c4ae6f9e4f66", "/equipment/assignments/create/ae408187-2e4a-41b2-85d9-c4ae6f9e4f66")]
    [InlineData("/equipment/assignments/create/invalid", "/dashboard")]
    public void Successful_login_returns_to_the_requested_equipment_form_or_default_dashboard(string? returnUrl, string expected)
    {
        using var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddDataProtection();
        context.Services.AddScoped<ProtectedSessionStorage>();
        context.Services.AddSingleton(DispatchProxy.Create<IAuthService, SuccessfulAuth>());
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo(returnUrl is null ? "/login" : "/login?returnUrl=" + Uri.EscapeDataString(returnUrl));
        var page = context.RenderComponent<Login>();
        page.Find("#csm-email").Change("supervisor@example.test");
        page.Find("#csm-password").Change("Test-Only-Password!2026");
        page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.EndsWith(expected, navigation.Uri));
        Assert.Empty(page.FindAll("[role=alert]"));
    }

    public class SuccessfulAuth : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            method!.Name == nameof(IAuthService.LoginAsync)
                ? Task.FromResult<LoginResponse?>(new() {
                    AccessToken = "test-access-token", RefreshToken = "test-refresh-token",
                    Email = "supervisor@example.test", FullName = "Test Supervisor",
                    Roles = ["Supervisor"]
                })
                : throw new InvalidOperationException("Unexpected authentication call.");
    }

    public class DeniedEquipment : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            Task.FromException<List<EquipmentResponse>>(new HttpRequestException("Unauthorized", null, HttpStatusCode.Unauthorized));
    }
}
