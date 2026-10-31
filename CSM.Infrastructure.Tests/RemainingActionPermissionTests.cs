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
}
