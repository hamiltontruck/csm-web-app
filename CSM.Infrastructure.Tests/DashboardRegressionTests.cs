using System.Net;
using System.Security.Claims;
using Bunit;
using CSM.Api.Controllers;
using CSM.Application.Dashboard.Dtos;
using CSM.Web.Components.Pages;
using CSM.Web.Models.Dashboard;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class DashboardRegressionTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("invalid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task MissingOrInvalidCompanyReturnsForbidden(string? company)
    {
        var service = new DashboardApiStub();
        var controller = new DashboardController(service, new DashboardCompanySelectionTests.CompaniesStub());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    company is null ? [] : new[] { new Claim("company_id", company) }, "test"))
            }
        };

        var result = Assert.IsType<ObjectResult>(
            await controller.GetExecutiveDashboard(default));
        Assert.Equal(403, result.StatusCode);
        Assert.Null(service.CompanyId);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, "server error")]
    [InlineData(HttpStatusCode.Forbidden, "company")]
    [InlineData(HttpStatusCode.Unauthorized, "sign in")]
    public async Task FailedApiRequestShowsErrorAndStopsLoading(HttpStatusCode status, string message)
    {
        using var context = new TestContext();
        var auth = System.Reflection.DispatchProxy.Create<IAuthService, WorkspaceVisibilityTests.Auth>();
        ((WorkspaceVisibilityTests.Auth)(object)auth).Role = "CompanyAdmin";
        ((WorkspaceVisibilityTests.Auth)(object)auth).Permissions = ["Reports_View", "Reports_FinancialSummary_View"];
        context.Services.AddSingleton(auth);
        context.Services.AddLogging();
        context.Services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        context.Services.AddScoped<ProtectedSessionStorage>();
        context.Services.AddSingleton<CSM.Web.Services.IExecutiveDashboardService>(
            new DashboardWebStub(status));
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var storage = context.Services.GetRequiredService<ProtectedSessionStorage>();
        await storage.SetAsync("jwt", "test-token");
        var encrypted = (string)context.JSInterop.Invocations["sessionStorage.setItem"].Single().Arguments[1]!;
        context.JSInterop.Setup<string>("sessionStorage.getItem", "jwt").SetResult(encrypted);

        var page = context.RenderComponent<Dashboard>();
        page.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("Loading Executive Dashboard", page.Markup);
            Assert.Contains(message, page.Find("[role=alert]").TextContent, StringComparison.OrdinalIgnoreCase);
        });
    }

    private sealed class DashboardApiStub : CSM.Application.Dashboard.IExecutiveDashboardService
    {
        public Guid? CompanyId { get; private set; }
        public Task<ExecutiveDashboardResponse> GetAsync(Guid companyId, CancellationToken cancellationToken = default)
        {
            CompanyId = companyId;
            return Task.FromResult(new ExecutiveDashboardResponse(1, 0, 0, 0, 0, 0, 0, 0, 0, DateTime.UtcNow));
        }
    }

    private sealed class DashboardWebStub(HttpStatusCode status) : CSM.Web.Services.IExecutiveDashboardService
    {
        public Task<IReadOnlyList<DashboardCompany>> GetCompaniesAsync() =>
            Task.FromResult<IReadOnlyList<DashboardCompany>>([]);
        public Task<ExecutiveDashboard?> GetDashboardAsync(Guid? selectedCompanyId = null) =>
            throw new HttpRequestException("API failed", null, status);
    }
}
