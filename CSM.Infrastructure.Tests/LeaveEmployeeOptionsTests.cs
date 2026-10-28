using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Bunit;
using CSM.Api.Controllers;
using CSM.Application.Common.Exceptions;
using CSM.Application.Common.Security;
using CSM.Domain.Entities.HRM;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using CSM.Web.Components.Pages.HR;
using CSM.Web.Models.HRM;
using CSM.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;
public sealed class LeaveEmployeeOptionsTests
{
    private static readonly Guid EmployeeId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Theory]
    [InlineData("SuperAdmin", true, true, true)] [InlineData("CompanyAdmin", true, true, true)]
    [InlineData("SiteManager", true, true, true)] [InlineData("Supervisor", true, true, true)]
    [InlineData("HrOfficer", true, true, true)] [InlineData("HROfficer", true, true, true)]
    [InlineData("Administrator", true, true, false)] [InlineData("Storekeeper", true, true, false)]
    [InlineData("Employee", true, true, false)] [InlineData("Supervisor", false, true, false)]
    [InlineData("SuperAdmin", false, true, false)] [InlineData("Supervisor", true, false, false)]
    public async Task Options_follow_leave_create_roles_company_and_active_actor_guards(string role, bool companyAssigned, bool active, bool allowed)
    {
        using var db = Database(role, companyAssigned, active, out var actor);
        var service = new CSM.Infrastructure.Services.LeaveService(db);
        if (!allowed)
        {
            await Assert.ThrowsAsync<LeaveManagementException>(() => Options(service, actor));
            return;
        }
        var result = await Options(service, actor);
        var row = Assert.Single(result.EnumerateArray());
        Assert.Equal(EmployeeId, row.GetProperty("Id").GetGuid());
        Assert.Equal("EMP-001", row.GetProperty("EmployeeNumber").GetString());
        Assert.Equal("Test Middle Employee", row.GetProperty("DisplayName").GetString());
        Assert.Equal(new[] { "DisplayName", "EmployeeNumber", "Id" }, row.EnumerateObject().Select(x => x.Name).Order().ToArray());
        Assert.Empty(db.LeaveRequests);
        Assert.DoesNotContain(db.ChangeTracker.Entries(), x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);
    }

    [Theory]
    [InlineData("SuperAdmin", true)] [InlineData("CompanyAdmin", true)] [InlineData("SiteManager", true)]
    [InlineData("Supervisor", true)] [InlineData("HrOfficer", true)] [InlineData("HROfficer", true)]
    [InlineData("Administrator", false)] [InlineData("Storekeeper", false)] [InlineData("Employee", false)]
    public async Task Lookup_route_requires_the_existing_leave_submit_roles(string role, bool expected)
    {
        var method = typeof(LeavesController).GetMethod("GetEmployeeOptions");
        Assert.NotNull(method);
        Assert.Equal("employee-options", method!.GetCustomAttribute<HttpGetAttribute>()!.Template);
        var metadata = typeof(LeavesController).GetCustomAttributes(true).OfType<IAuthorizeData>()
            .Concat(method.GetCustomAttributes(true).OfType<IAuthorizeData>());
        var services = new ServiceCollection().AddLogging().AddAuthorization().BuildServiceProvider();
        using (services)
        {
            var policy = await AuthorizationPolicy.CombineAsync(services.GetRequiredService<IAuthorizationPolicyProvider>(), metadata);
            var actor = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "test"));
            Assert.Equal(expected, (await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(actor, null, policy!)).Succeeded);
        }
    }

    [Fact]
    public async Task General_employee_API_remains_denied_to_supervisor()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddAuthorization(o => o.AddPolicy(AppPolicies.HRManagement, p => p.RequireRole(AppPolicies.HRManagementRoles)));
        using var provider = services.BuildServiceProvider();
        var metadata = typeof(EmployeesController).GetCustomAttributes(true).OfType<IAuthorizeData>();
        var policy = await AuthorizationPolicy.CombineAsync(provider.GetRequiredService<IAuthorizationPolicyProvider>(), metadata);
        var actor = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Supervisor")], "test"));
        Assert.False((await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(actor, null, policy!)).Succeeded);
    }

    [Fact]
    public void Form_uses_minimal_leave_lookup_while_general_employee_API_is_denied()
    {
        using var context = Context(new LookupHandler());
        var page = context.RenderComponent<LeaveCreate>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("form")));
        var option = page.FindAll("option").Single(x => x.GetAttribute("value") == EmployeeId.ToString());
        Assert.Contains("EMP-001", option.TextContent);
        Assert.Contains("Test Employee", option.TextContent);
        Assert.DoesNotContain("secret", page.Markup);
        Assert.Empty(page.FindAll("[role=alert]"));
    }

    [Fact]
    public void Empty_allowed_lookup_explains_no_active_employees_and_has_no_submit_form()
    {
        using var context = Context(new LookupHandler { Empty = true });
        var page = context.RenderComponent<LeaveCreate>();
        page.WaitForAssertion(() => Assert.Contains("No active employees are available for leave requests.", page.Markup));
        Assert.Empty(page.FindAll("form, input, select, textarea"));
        Assert.Empty(page.FindAll("[role=alert]"));
    }

    [Theory]
    [InlineData(401)] [InlineData(403)] [InlineData(500)]
    public void Lookup_errors_keep_safe_recovery_and_no_partial_form(int status)
    {
        using var context = Context(new LookupHandler { Status = (HttpStatusCode)status });
        var page = context.RenderComponent<LeaveCreate>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("[role=alert]")));
        Assert.Empty(page.FindAll("form, input, select, textarea"));
        Assert.Equal(status == 401, page.FindAll("#leave-create-sign-in").Count == 1);
        Assert.Equal(status == 500, page.FindAll("#retry-leave-create").Count == 1);
        Assert.DoesNotContain("secret", page.Markup);
    }

    private static async Task<JsonElement> Options(CSM.Infrastructure.Services.LeaveService service, Guid actor)
    {
        var method = service.GetType().GetMethod("GetEmployeeOptionsAsync");
        Assert.NotNull(method);
        var task = (Task)method!.Invoke(service, new object[] { actor, CancellationToken.None })!;
        await task;
        return JsonSerializer.SerializeToElement(task.GetType().GetProperty("Result")!.GetValue(task));
    }
    private static ApplicationDbContext Database(string role, bool companyAssigned, bool active, out Guid actor)
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var company = Guid.NewGuid(); actor = Guid.NewGuid();
        db.Users.Add(new User { Id = actor, CompanyId = companyAssigned ? company : null, IsActive = active, UserRoles = [
            new UserRole { Id = Guid.NewGuid(), Role = new Role { Id = Guid.NewGuid(), Name = role } } ] });
        db.Employees.Add(new Employee { Id = EmployeeId, CompanyId = company, EmployeeNumber = "EMP-001",
            FirstName = "Test", MiddleName = "Middle", LastName = "Employee", Status = EmployeeStatus.Active,
            BaseWage = 12345, Email = "secret@example.test", PhoneNumber = "secret", BankAccountNumber = "secret" });
        db.Employees.Add(new Employee { Id = Guid.NewGuid(), CompanyId = Guid.NewGuid(), EmployeeNumber = "FOREIGN",
            FirstName = "Foreign", LastName = "Employee", Status = EmployeeStatus.Active });
        db.Employees.Add(new Employee { Id = Guid.NewGuid(), CompanyId = company, EmployeeNumber = "INACTIVE",
            FirstName = "Inactive", LastName = "Employee", Status = Enum.GetValues<EmployeeStatus>().First(x => x != EmployeeStatus.Active) });
        db.Employees.Add(new Employee { Id = Guid.NewGuid(), CompanyId = company, EmployeeNumber = "DELETED",
            FirstName = "Deleted", LastName = "Employee", Status = EmployeeStatus.Active, IsDeleted = true });
        db.SaveChanges(true); return db;
    }
    private static TestContext Context(LookupHandler handler)
    {
        var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddDataProtection();
        context.Services.AddScoped<ProtectedSessionStorage>();
        context.Services.AddSingleton<IHttpClientFactory>(new Factory(new HttpClient(handler) { BaseAddress = new Uri("https://api.test/api/") }));
        context.Services.AddScoped<ILeaveService, CSM.Web.Services.LeaveService>();
        context.Services.AddSingleton(DispatchProxy.Create<IEmployeeService, DeniedEmployees>());
        context.Services.AddSingleton(DispatchProxy.Create<IAuthService, Auth>());
        return context;
    }
    public class DeniedEmployees : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            Task.FromException<IReadOnlyList<EmployeeResponse>>(new HttpRequestException("secret", null, HttpStatusCode.Forbidden));
    }
    public class Auth : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            Task.FromResult<IReadOnlyCollection<string>>(new[] { "HRM_Leave_Submit" });
    }
    private sealed class Factory(HttpClient http) : IHttpClientFactory { public HttpClient CreateClient(string name) => http; }
    private sealed class LookupHandler : HttpMessageHandler
    {
        public HttpStatusCode? Status; public bool Empty;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            if (request.Method != HttpMethod.Get) throw new InvalidOperationException("Unexpected leave write.");
            if (request.RequestUri!.AbsolutePath == "/api/Leaves")
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") });
            Assert.Equal("/api/Leaves/employee-options", request.RequestUri.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(Status ?? HttpStatusCode.OK) {
                Content = new StringContent(Status.HasValue ? "secret" : Empty ? "[]" :
                    "[{\"id\":\"11111111-1111-1111-1111-111111111111\",\"employeeNumber\":\"EMP-001\",\"displayName\":\"Test Employee\"}]",
                    System.Text.Encoding.UTF8, "application/json") });
        }
    }
}
