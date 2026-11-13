using System.Net;
using System.Reflection;
using Bunit;
using CSM.Web.Models.Finance;
using CSM.Web.Models.Projects;
using CSM.Web.Models.Sites;
using CSM.Web.Models.Procurement;
using CSM.Web.Services;
using CSM.Web.Services.Access;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class ExpenseDetailLookupTests
{
    [Theory]
    [InlineData(false, "sites")] [InlineData(false, "projects")]
    [InlineData(false, "vendors")] [InlineData(false, "costcodes")]
    [InlineData(true, "sites")] [InlineData(true, "projects")]
    [InlineData(true, "vendors")] [InlineData(true, "costcodes")]
    public void Authorized_expense_remains_visible_when_related_lookup_is_denied(bool companyPage, string denied)
    {
        using var context = new TestContext(); var state = Services(context); state.DeniedLookup = denied;
        var page = Render(context, state, companyPage);
        page.WaitForAssertion(() => Assert.Contains("EXP-0001", page.Markup));
        Assert.Contains("1,000.00", page.Markup);
        Assert.Contains("Some related names are unavailable", page.Markup);
        Assert.DoesNotContain("Expense not found", page.Markup);
        Assert.DoesNotContain("private diagnostic", page.Markup);
    }

    [Theory]
    [InlineData(false, 401)] [InlineData(false, 403)] [InlineData(false, 404)] [InlineData(false, 500)]
    [InlineData(true, 401)] [InlineData(true, 403)] [InlineData(true, 404)] [InlineData(true, 500)]
    public void Primary_expense_failure_is_not_misreported_as_missing(bool companyPage, int status)
    {
        using var context = new TestContext(); var state = Services(context); state.PrimaryStatus = (HttpStatusCode)status;
        var page = Render(context, state, companyPage);
        page.WaitForAssertion(() => Assert.Single(page.FindAll("[role=alert]")));
        Assert.DoesNotContain("EXP-0001", page.Markup);
        Assert.DoesNotContain("private diagnostic", page.Markup);
        var expected = status switch {
            401 => "Your session is missing or has expired",
            403 => "You do not have permission to view this expense",
            404 => "Expense not found",
            _ => "Unable to load expense details"
        };
        Assert.Contains(expected, page.Markup);
        Assert.Equal(status == 404, page.Markup.Contains("Expense not found"));
        Assert.Contains(page.FindAll("button"), b => b.TextContent.Contains("Expenses"));
        Assert.Empty(state.RelatedCalls);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Site_manager_cannot_see_paid_control_on_approved_expense(bool companyPage)
    {
        using var context = new TestContext(); var state = Services(context);
        var page = Render(context, state, companyPage);
        page.WaitForAssertion(() => Assert.Contains("EXP-0001", page.Markup));
        Assert.DoesNotContain(page.FindAll("button"), b => b.TextContent.Contains("Mark as Paid"));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Finance_manager_cannot_mark_paid_without_linked_disbursement(bool companyPage)
    {
        using var context = new TestContext(); var state = Services(context);
        var page = Render(context, state, companyPage, "CompanyAdmin", "Finance_Expense_View,Finance_Expense_Approve");
        page.WaitForAssertion(() => Assert.Contains("EXP-0001", page.Markup));
        Assert.DoesNotContain(page.FindAll("button"), b => b.TextContent.Contains("Mark as Paid"));
    }

    [Theory]
    [InlineData(false, 401)] [InlineData(true, 401)]
    [InlineData(false, 404)] [InlineData(true, 404)]
    [InlineData(false, 500)] [InlineData(true, 500)]
    public void Related_lookup_expiry_withholds_data_while_other_lookup_failures_preserve_it(bool companyPage, int status)
    {
        using var context = new TestContext(); var state = Services(context);
        state.DeniedLookup = "costcodes"; state.RelatedStatus = (HttpStatusCode)status;
        var page = Render(context, state, companyPage);
        if (status == 401) {
            page.WaitForAssertion(() => Assert.Contains("Your session is missing or has expired", page.Markup));
            Assert.DoesNotContain("EXP-0001", page.Markup);
        } else {
            page.WaitForAssertion(() => Assert.Contains("EXP-0001", page.Markup));
            Assert.Contains("Some related names are unavailable", page.Markup);
        }
        Assert.DoesNotContain("private diagnostic", page.Markup);
    }

    [Theory]
    [InlineData("SiteManager", "Finance_Expense_View,Finance_Expense_Approve")]
    [InlineData("CompanyAdmin", "Finance_Expense_View")]
    public void Paid_action_requires_both_approval_permission_and_finance_manager_role(string role, string permissions)
    {
        using var context = new TestContext(); var state = Services(context);
        var page = Render(context, state, false, role, permissions);
        page.WaitForAssertion(() => Assert.Contains("EXP-0001", page.Markup));
        Assert.DoesNotContain(page.FindAll("button"), b => b.TextContent.Contains("Mark as Paid"));
    }

    [Fact]
    public void Wrong_site_route_withholds_expense_and_related_requests()
    {
        using var context = new TestContext(); var state = Services(context); state.WrongSite = true;
        var page = Render(context, state, false);
        page.WaitForAssertion(() => Assert.Contains("Expense not found", page.Markup));
        Assert.DoesNotContain("EXP-0001", page.Markup); Assert.Empty(state.RelatedCalls);
    }

    private static Probe Services(TestContext context)
    {
        var finance = DispatchProxy.Create<IFinanceService, Probe>();
        var state = (Probe)(object)finance;
        context.Services.AddSingleton(finance);
        var project = DispatchProxy.Create<IProjectService, ProjectProbe>(); ((ProjectProbe)(object)project).State = state;
        var vendor = DispatchProxy.Create<IVendorService, VendorProbe>(); ((VendorProbe)(object)vendor).State = state;
        context.Services.AddSingleton(project); context.Services.AddSingleton(vendor);
        return state;
    }

    private static IRenderedFragment Render(TestContext context, Probe state, bool companyPage,
        string role = "SiteManager", string permissions = "Finance_Expense_View,Finance_Expense_Submit")
        => context.RenderComponent<CascadingValue<WorkspaceAccess>>(p => p.Add(x => x.Value,
            new WorkspaceAccess([role], permissions.Split(','))).Add(x => x.ChildContent, builder => {
                if (companyPage) {
                    builder.OpenComponent<CSM.Web.Components.Pages.Finance.Company.ExpenseDetails>(0);
                    builder.AddAttribute(1, "id", state.ExpenseId);
                } else {
                    builder.OpenComponent<CSM.Web.Components.Pages.Finance.Sites.SiteExpenseDetails>(0);
                    builder.AddAttribute(1, "ExpenseId", state.ExpenseId); builder.AddAttribute(2, "SiteId", state.SiteId);
                }
                builder.CloseComponent();
            }));

    public class Probe : DispatchProxy
    {
        public Guid ExpenseId = Guid.NewGuid(), SiteId = Guid.NewGuid();
        public string? DeniedLookup; public HttpStatusCode? PrimaryStatus; public bool WrongSite;
        public HttpStatusCode RelatedStatus = HttpStatusCode.Forbidden;
        public List<string> RelatedCalls = [];
        public void Check(string lookup) { RelatedCalls.Add(lookup); if (DeniedLookup == lookup) throw Error(RelatedStatus); }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == nameof(IFinanceService.GetExpenseAsync)) {
                Assert.Equal(ExpenseId, args![0]);
                return PrimaryStatus.HasValue ? Task.FromException<ExpenseResponse>(Error(PrimaryStatus.Value)) :
                    Task.FromResult(new ExpenseResponse(ExpenseId, Guid.NewGuid(), WrongSite ? Guid.NewGuid() : SiteId,
                        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "EXP-0001", new(2026,10,18), "Site expense",
                        1000, 0, "ETB", 1, null, null, "Approved", null, null, null, DateTime.UtcNow, null));
            }
            if (method.Name == nameof(IFinanceService.GetActiveCostCodesAsync)) { Check("costcodes"); return Task.FromResult<IReadOnlyList<CostCodeResponse>>([]); }
            throw new NotSupportedException(method.Name);
        }
    }
    public class ProjectProbe : DispatchProxy
    {
        public Probe State = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == nameof(IProjectService.GetSitesAsync)) { State.Check("sites"); return Task.FromResult(new List<SiteResponse>()); }
            if (method.Name == nameof(IProjectService.GetProjectsBySiteAsync)) { State.Check("projects"); return Task.FromResult(new List<ProjectResponse>()); }
            throw new NotSupportedException(method.Name);
        }
    }
    public class VendorProbe : DispatchProxy
    {
        public Probe State = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) { State.Check("vendors"); return Task.FromResult(new List<VendorResponse>()); }
    }
    private static HttpRequestException Error(HttpStatusCode status) => new("private diagnostic", null, status);
}
