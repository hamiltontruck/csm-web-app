using System.Net;
using System.Reflection;
using Bunit;
using CSM.Web.Components.Pages.HR;
using CSM.Web.Models.HRM;
using CSM.Web.Services;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class PayrollFeedbackTests
{
    [Theory]
    [InlineData("list", 401)] [InlineData("list", 403)] [InlineData("list", 500)]
    [InlineData("create", 401)] [InlineData("create", 403)] [InlineData("create", 500)]
    [InlineData("edit", 401)] [InlineData("edit", 403)] [InlineData("edit", 500)]
    public void Failed_load_hides_records_and_partial_forms(string route, int status)
    {
        using var context = Context(out var state);
        state.Status = (HttpStatusCode)status;
        var page = Render(context, route);
        page.WaitForAssertion(() => Assert.Single(page.FindAll("[role=alert]")));
        Assert.Empty(page.FindAll("table, form, input, select, textarea"));
        Assert.DoesNotContain("No payroll records", page.Markup);
        Assert.DoesNotContain("private diagnostic", page.Markup);
        Assert.DoesNotContain(page.FindAll("button"), b => b.TextContent.Trim() == "Create Payroll");
        Assert.Equal(status == 401, page.FindAll("#payroll-" + route + "-sign-in").Count == 1);
        Assert.Equal(status == 500, page.FindAll("#retry-payroll-" + route).Count == 1);
    }

    [Theory]
    [InlineData("list")] [InlineData("create")] [InlineData("edit")]
    public void Retry_recovers_without_writing_payroll(string route)
    {
        using var context = Context(out var state);
        state.Status = HttpStatusCode.ServiceUnavailable;
        var page = Render(context, route);
        page.WaitForAssertion(() => Assert.Single(page.FindAll("#retry-payroll-" + route)));
        state.Status = null;
        page.Find("#retry-payroll-" + route).Click();
        page.WaitForAssertion(() => Assert.Contains("Test Employee", page.Markup));
        Assert.Empty(page.FindAll("[role=alert]"));
    }

    [Fact]
    public void Supervisor_can_read_but_cannot_create_or_calculate()
    {
        using var context = Context(out var state);
        state.Role = "Supervisor";
        var list = Render(context, "list");
        list.WaitForAssertion(() => Assert.Contains("Test Employee", list.Markup));
        Assert.DoesNotContain(list.FindAll("button"), b => b.TextContent.Trim() == "Create Payroll");
        var detail = Render(context, "edit");
        detail.WaitForAssertion(() => Assert.Contains("Test Employee", detail.Markup));
        Assert.Empty(detail.FindAll("form"));
        var create = Render(context, "create");
        create.WaitForAssertion(() => Assert.Single(create.FindAll("[role=alert]")));
        Assert.Empty(create.FindAll("form, input, select, textarea, #retry-payroll-create"));
    }

    [Fact]
    public void Employee_lookup_denial_hides_form_and_has_no_retry()
    {
        using var context = Context(out var state);
        state.OptionsDenied = true;
        var page = Render(context, "create");
        page.WaitForAssertion(() => Assert.Single(page.FindAll("[role=alert]")));
        Assert.Empty(page.FindAll("form, input, select, textarea, #retry-payroll-create"));
        Assert.DoesNotContain("private diagnostic", page.Markup);
    }

    [Fact]
    public void Missing_detail_has_no_form_or_retry()
    {
        using var context = Context(out var state);
        state.Missing = true;
        var page = Render(context, "edit");
        page.WaitForAssertion(() => Assert.Contains("Payroll record was not found.", page.Markup));
        Assert.Empty(page.FindAll("form, input, textarea, #retry-payroll-edit"));
    }

    [Fact]
    public void Denied_calculation_clears_stale_controls_without_crashing()
    {
        using var context = Context(out var state);
        state.ActionDenied = true;
        var page = Render(context, "edit");
        page.WaitForAssertion(() => Assert.Single(page.FindAll("form")));
        page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("[role=alert]")));
        Assert.Empty(page.FindAll("form, input, textarea"));
        Assert.DoesNotContain("private diagnostic", page.Markup);
    }

    [Theory]
    [InlineData("Supervisor", true, false)]
    [InlineData("CompanyAdmin", false, false)]
    [InlineData("CompanyAdmin", true, true)]
    public void Approval_controls_require_service_role_and_existing_approval_permission(string role, bool permission, bool expected)
    {
        using var context = Context(out var state);
        state.Role = role; state.ApprovePermission = permission; state.RecordStatus = "Calculated";
        var page = Render(context, "edit");
        page.WaitForAssertion(() => Assert.Contains("Test Employee", page.Markup));
        Assert.Equal(expected, page.FindAll("button").Any(b => b.TextContent.Trim() == "Approve Payroll"));
    }

    [Theory]
    [InlineData("list", 401)] [InlineData("list", 403)] [InlineData("list", 500)]
    [InlineData("detail", 401)] [InlineData("detail", 403)] [InlineData("detail", 500)]
    [InlineData("calculate", 401)] [InlineData("calculate", 403)] [InlineData("calculate", 500)]
    public async Task Clients_preserve_HTTP_status_without_response_body(string method, int status)
    {
        using var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddDataProtection();
        context.Services.AddScoped<ProtectedSessionStorage>();
        using var http = new HttpClient(new Response((HttpStatusCode)status)) { BaseAddress = new Uri("https://api.test/api/") };
        var service = new CSM.Web.Services.PayrollService(new Factory(http), context.Services.GetRequiredService<ProtectedSessionStorage>());
        var error = await Assert.ThrowsAsync<HttpRequestException>(async () => {
            if (method == "list") await service.GetAllAsync();
            else if (method == "detail") await service.GetByIdAsync(Guid.NewGuid());
            else await service.CalculateAsync(Guid.NewGuid(), new());
        });
        Assert.Equal((HttpStatusCode)status, error.StatusCode);
        Assert.DoesNotContain("private diagnostic", error.Message);
    }

    private static IRenderedFragment Render(TestContext context, string route) => route switch {
        "list" => context.RenderComponent<Payroll>(),
        "create" => context.RenderComponent<PayrollCreate>(),
        _ => context.RenderComponent<PayrollEdit>(p => p.Add(x => x.PayrollId, Guid.Parse("11111111-1111-1111-1111-111111111111")))
    };
    private static TestContext Context(out State state)
    {
        var context = new TestContext(); state = new();
        var api = DispatchProxy.Create<IPayrollService, Api>(); ((Api)(object)api).State = state;
        var employee = DispatchProxy.Create<IEmployeeService, EmployeeApi>(); ((EmployeeApi)(object)employee).State = state;
        var auth = DispatchProxy.Create<IAuthService, Auth>(); ((Auth)(object)auth).State = state;
        context.Services.AddSingleton(api); context.Services.AddSingleton(employee); context.Services.AddSingleton(auth);
        return context;
    }
    public sealed class State { public HttpStatusCode? Status; public string Role = "CompanyAdmin"; public bool ApprovePermission = true; public string RecordStatus = "Draft"; public bool OptionsDenied; public bool Missing; public bool ActionDenied; }
    public class Api : DispatchProxy
    {
        public State State = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            var row = new PayrollResponse { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), EmployeeName = "Test Employee", Status = State.RecordStatus };
            if (method!.Name == nameof(IPayrollService.GetAllAsync))
                return State.Status.HasValue ? Task.FromException<IReadOnlyCollection<PayrollResponse>>(Error(State.Status.Value))
                    : Task.FromResult<IReadOnlyCollection<PayrollResponse>>(new[] { row });
            if (method.Name == nameof(IPayrollService.GetByIdAsync))
                return State.Status.HasValue ? Task.FromException<PayrollResponse?>(Error(State.Status.Value))
                    : Task.FromResult<PayrollResponse?>(State.Missing ? null : row);
            if (State.ActionDenied) return Task.FromException<PayrollResponse>(Error(HttpStatusCode.Forbidden));
            throw new InvalidOperationException("Unexpected payroll mutation.");
        }
    }
    public class EmployeeApi : DispatchProxy
    {
        public State State = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => State.OptionsDenied
            ? Task.FromException<IReadOnlyList<EmployeeResponse>>(Error(HttpStatusCode.Forbidden))
            : Task.FromResult<IReadOnlyList<EmployeeResponse>>(new[] { new EmployeeResponse { FirstName = "Test Employee" } });
    }
    public class Auth : DispatchProxy
    {
        public State State = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            Task.FromResult<IReadOnlyCollection<string>>(method!.Name == nameof(IAuthService.GetRolesAsync)
                ? new[] { State.Role } : State.ApprovePermission ? new[] { "HRM_Payroll_View", "HRM_Payroll_Approve", "HRM_Employee_View" } : new[] { "HRM_Payroll_View", "HRM_Employee_View" });
    }
    private static HttpRequestException Error(HttpStatusCode status) => new("private diagnostic", null, status);
    private sealed class Factory(HttpClient http) : IHttpClientFactory { public HttpClient CreateClient(string name) => http; }
    private sealed class Response(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent("private diagnostic") });
    }
}
