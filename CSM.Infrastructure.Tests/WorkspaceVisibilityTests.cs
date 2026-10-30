using System.Reflection;
using Bunit;
using CSM.Web.Components.Pages;
using CSM.Web.Models.HRM;
using CSM.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class WorkspaceVisibilityTests
{
    [Fact]
    public void Supervisor_HR_hides_forbidden_cards_and_does_not_request_contractors()
    {
        using var context = Context("Supervisor", "HRM_Employee_View", "HRM_Contractor_View",
            "HRM_Attendance_View", "HRM_Payroll_View", "HRM_Leave_View", "HRM_Shift_View",
            "HRM_SiteAssignment_View");
        var contractors = DispatchProxy.Create<IContractorService, Contractors>();
        var calls = (Contractors)(object)contractors;
        context.Services.AddSingleton(contractors);
        var page = context.RenderComponent<Hr>();
        page.WaitForAssertion(() => Assert.Contains("Manage Attendances", page.Markup));
        Assert.DoesNotContain(page.FindAll("h6"), h => h.TextContent == "Employees");
        Assert.DoesNotContain(page.FindAll("h6"), h => h.TextContent == "Contractors");
        Assert.Contains("Manage Payroll", page.Markup);
        Assert.Contains("Manage Timesheets", page.Markup);
        Assert.Equal(0, calls.Reads);
    }

    [Fact]
    public void Supervisor_equipment_hub_hides_modules_rejected_by_the_API()
    {
        using var context = Context("Supervisor", "Procurement_Inventory_View");
        var page = context.RenderComponent<Equipment>();
        page.WaitForAssertion(() => Assert.DoesNotContain(page.FindAll("h4"), h => h.TextContent == "Assignments"));
        Assert.DoesNotContain(page.FindAll("h4"), h => h.TextContent == "Maintenance");
        Assert.DoesNotContain(page.FindAll("h4"), h => h.TextContent == "Downtime");
        Assert.DoesNotContain(page.FindAll("h4"), h => h.TextContent == "Fuel Consumption");
        Assert.DoesNotContain(page.FindAll("h4"), h => h.TextContent == "Daily Timesheets");
    }

    [Fact]
    public void Supervisor_compliance_hides_forbidden_cards_without_reading_safety_data()
    {
        using var context = Context("Supervisor", "Compliance_View");
        var api = DispatchProxy.Create<IComplianceService, Unexpected>();
        context.Services.AddSingleton(api);
        var page = context.RenderComponent<Compliance>();
        page.WaitForAssertion(() => Assert.DoesNotContain("Manage Documents", page.Markup));
        Assert.DoesNotContain("Manage Categories", page.Markup);
        Assert.DoesNotContain("View Safety Incidents", page.Markup);
    }

    private static TestContext Context(string role, params string[] permissions)
    {
        var context = new TestContext();
        var auth = DispatchProxy.Create<IAuthService, Auth>();
        ((Auth)(object)auth).Role = role;
        ((Auth)(object)auth).Permissions = permissions;
        context.Services.AddSingleton(auth);
        return context;
    }

    public class Auth : DispatchProxy
    {
        public string Role = "";
        public string[] Permissions = [];
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            method!.Name == nameof(IAuthService.GetRolesAsync)
                ? Task.FromResult<IReadOnlyCollection<string>>(new[] { Role })
                : method.Name == nameof(IAuthService.GetPermissionsAsync)
                    ? Task.FromResult<IReadOnlyCollection<string>>(Permissions)
                    : throw new InvalidOperationException("Unexpected auth request.");
    }
    public class Contractors : DispatchProxy
    {
        public int Reads;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name != nameof(IContractorService.GetContractorsAsync))
                throw new InvalidOperationException("Unexpected contractor mutation.");
            Reads++;
            return Task.FromResult<IReadOnlyList<ContractorResponse>>([]);
        }
    }
    public class Unexpected : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            throw new InvalidOperationException("A hidden module must not load data.");
    }
}
