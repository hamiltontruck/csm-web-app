using System.Reflection;
using System.Security.Claims;
using CSM.Application.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using CSM.Domain.Enums;
using Xunit;
namespace CSM.Infrastructure.Tests;
public sealed class RemainingActionPermissionTests
{
    public static IEnumerable<object[]> Actions
    {
        get
        {
            foreach (var action in new[] { "Create", "Update", "ChangeStatus", "Issue", "Cancel", "Print" })
                yield return ["InvoicesController", action, GranularPermission.Finance_Invoice_View, GranularPermission.Finance_Invoice_View];
            foreach (var action in new[] { "GetByInvoice", "GetById", "Create", "Update", "Delete" })
                yield return ["InvoiceLinesController", action, GranularPermission.Finance_Invoice_View, GranularPermission.Finance_Invoice_View];
            foreach (var action in new[] { "Create", "Calculate", "Cancel", "AddAdjustment" })
                yield return ["PayrollController", action, GranularPermission.HRM_Payroll_View, GranularPermission.HRM_Payroll_View];
            foreach (var action in new[] { "GetAll", "GetUnreadCount", "MarkRead" })
                yield return ["NotificationsController", action, GranularPermission.Notifications_View, GranularPermission.Notifications_View];
            yield return ["NotificationsController", "Create", GranularPermission.Notifications_Manage, GranularPermission.Notifications_View];
        }
    }
    [Theory]
    [MemberData(nameof(Actions))]
    public Task HTTP_action_checks_current_grant_before_entering_eligible_role_service(string controller, string action, GranularPermission required, GranularPermission read)
        => new BackendActionPermissionTests().HTTP_action_requires_its_own_permission_even_when_all_role_checks_pass(controller, action, required, read);
    [Theory]
    [InlineData("SuperAdmin", true)]
    [InlineData("CompanyAdmin", true)]
    [InlineData("Accountant", true)]
    [InlineData("Administrator", false)]
    [InlineData("FinanceOfficer", false)]
    [InlineData("Supervisor", false)]
    public async Task Invoice_line_API_matches_existing_invoice_service_roles(string role, bool allowed)
    {
        var registration = new ServiceCollection().AddLogging().AddGrantedBackendPermissions(role, [GranularPermission.Finance_Invoice_View]);
        registration.AddAuthorization(options => options.AddPolicy(AppPolicies.FinanceManagement, p => p.RequireRole(AppPolicies.FinanceManagementRoles)));
        using var services = registration.BuildServiceProvider();
        var controller = typeof(CSM.Api.Controllers.InvoiceLinesController);
        var metadata = controller.GetCustomAttributes<AuthorizeAttribute>().Concat(controller.GetMethod("GetByInvoice")!.GetCustomAttributes<AuthorizeAttribute>());
        var policy = await AuthorizationPolicy.CombineAsync(services.GetRequiredService<IAuthorizationPolicyProvider>(), metadata);
        var actor = BackendAuthorizationFixture.WithGrantedIdentity(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "test")));
        Assert.Equal(allowed, (await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(actor, null, policy!)).Succeeded);
    }
}
