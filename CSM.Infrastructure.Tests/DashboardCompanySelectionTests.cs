using System.Security.Claims;
using CSM.Api.Controllers;
using CSM.Application.CompanySettings;
using CSM.Application.CompanySettings.Dtos;
using CSM.Application.Dashboard;
using CSM.Application.Dashboard.Dtos;
using CSM.Domain.Constants;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class DashboardCompanySelectionTests
{
    [Theory]
    [InlineData("company_id")]
    [InlineData("CompanyId")]
    [InlineData("companyId")]
    public async Task CompanyUserLoadsOnlyTheirCompany(string claimName)
    {
        var companyId = Guid.NewGuid();
        var (controller, dashboard) = CreateController(new Claim(claimName, companyId.ToString()));
        Assert.IsType<OkObjectResult>(await controller.GetExecutiveDashboard(default));
        Assert.Equal(companyId, dashboard.CompanyId);
    }

    [Theory]
    [InlineData(AppRoles.CompanyAdmin)]
    [InlineData(AppRoles.Administrator)]
    [InlineData(AppRoles.Employee)]
    public async Task CompanyUserCannotRequestAnotherCompany(string role)
    {
        var (controller, dashboard) = CreateController(
            new Claim("company_id", Guid.NewGuid().ToString()), new Claim(ClaimTypes.Role, role));
        var result = Assert.IsType<ForbidResult>(
            await controller.GetExecutiveDashboard(default, Guid.NewGuid()));
        Assert.Null(dashboard.CompanyId);
    }

    [Fact]
    public async Task SuperAdminWithoutCompanyCanLoadSelectedCompany()
    {
        var (controller, dashboard) = CreateController(new Claim(ClaimTypes.Role, AppRoles.SuperAdmin));
        Assert.IsType<OkObjectResult>(await controller.GetExecutiveDashboard(default, CompaniesStub.Id));
        Assert.Equal(CompaniesStub.Id, dashboard.CompanyId);
    }

    [Fact]
    public async Task UnknownSelectedCompanyReturnsNotFound()
    {
        var (controller, dashboard) = CreateController(new Claim(ClaimTypes.Role, AppRoles.SuperAdmin));
        Assert.IsType<NotFoundObjectResult>(await controller.GetExecutiveDashboard(default, Guid.NewGuid()));
        Assert.Null(dashboard.CompanyId);
    }

    private static (DashboardController Controller, DashboardStub Dashboard) CreateController(params Claim[] claims)
    {
        var dashboard = new DashboardStub();
        var controller = new DashboardController(dashboard, new CompaniesStub())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
                }
            }
        };
        return (controller, dashboard);
    }

    internal sealed class CompaniesStub : ICompanySettingsService
    {
        public static readonly Guid Id = Guid.Parse("c44a3229-789b-440c-9e1d-906ee466fd56");
        public Task<IReadOnlyList<CompanyLookupDto>> GetCompaniesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CompanyLookupDto>>([new() { Id = Id, Name = "Test Company" }]);
        public Task<GetCompanySettingsResponse> GetAsync(Guid id, CancellationToken token) => throw new NotSupportedException();
        public Task<GetCompanySettingsResponse> GetByCompanyIdAsync(Guid id, CancellationToken token) => throw new NotSupportedException();
        public Task<GetCompanySettingsResponse> UpdateAsync(Guid id, UpdateCompanySettingsRequest request, CancellationToken token) => throw new NotSupportedException();
    }

    private sealed class DashboardStub : IExecutiveDashboardService
    {
        public Guid? CompanyId { get; private set; }
        public Task<ExecutiveDashboardResponse> GetAsync(Guid id, CancellationToken cancellationToken = default)
        {
            CompanyId = id;
            return Task.FromResult(new ExecutiveDashboardResponse(1, 0, 0, 0, 0, 0, 0, 0, 0, DateTime.UtcNow));
        }
    }
}
