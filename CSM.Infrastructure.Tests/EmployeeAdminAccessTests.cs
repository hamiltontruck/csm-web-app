using System.Reflection;
using System.Security.Claims;
using CSM.Api.Controllers;
using CSM.Application.Common.Security;
using CSM.Application.Common.Exceptions;
using CSM.Domain.Entities.HRM;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class EmployeeAdminAccessTests
{
    [Theory]
    [InlineData("SuperAdmin", true)]
    [InlineData("CompanyAdmin", true)]
    [InlineData("HrOfficer", true)]
    [InlineData("HROfficer", true)]
    [InlineData("Supervisor", false)]
    [InlineData("SiteManager", false)]
    [InlineData("Administrator", false)]
    [InlineData("Storekeeper", false)]
    [InlineData("Employee", false)]
    public async Task Employee_endpoints_allow_existing_admin_and_HR_managers_only(string role, bool expected)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddAuthorization(o => o.AddPolicy(AppPolicies.HRManagement, p => p.RequireRole(AppPolicies.HRManagementRoles)));
        using var provider = services.AddGrantedBackendPermissions(role).BuildServiceProvider();
        var actor = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "test"));
        foreach (var name in new[] { "GetAll", "GetById", "Create", "Update", "ChangeStatus" })
        {
            var metadata = typeof(EmployeesController).GetCustomAttributes(true).OfType<IAuthorizeData>()
                .Concat(typeof(EmployeesController).GetMethod(name)!.GetCustomAttributes(true).OfType<IAuthorizeData>());
            var policy = await AuthorizationPolicy.CombineAsync(provider.GetRequiredService<IAuthorizationPolicyProvider>(), metadata);
            Assert.Equal(expected, (await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(BackendAuthorizationFixture.WithGrantedIdentity(actor), null, policy!)).Succeeded);
        }
    }

    [Theory]
    [InlineData("SuperAdmin", false, true, 2)]
    [InlineData("CompanyAdmin", true, true, 1)]
    [InlineData("CompanyAdmin", false, true, -1)]
    [InlineData("SuperAdmin", false, false, -1)]
    public async Task Admin_read_keeps_existing_company_and_active_actor_guards(string role, bool assigned, bool active, int count)
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var company = Guid.NewGuid(); var actor = Guid.NewGuid();
        db.Users.Add(new User { Id = actor, CompanyId = assigned ? company : null, IsActive = active,
            UserRoles = [new UserRole { Id = Guid.NewGuid(), Role = new Role { Id = Guid.NewGuid(), Name = role } }] });
        foreach (var id in new[] { company, Guid.NewGuid() })
            db.Employees.Add(new Employee { Id = Guid.NewGuid(), CompanyId = id, EmployeeNumber = id.ToString(),
                FirstName = "Test", LastName = "Employee", Status = EmployeeStatus.Active });
        db.SaveChanges(true);
        var service = new CSM.Infrastructure.Services.EmployeeService(db);
        if (count < 0)
            await Assert.ThrowsAsync<EmployeeManagementException>(() => service.GetAllAsync(actor));
        else
        {
            var rows = await service.GetAllAsync(actor);
            Assert.Equal(count, rows.Count);
            if (assigned) Assert.All(rows, row => Assert.Equal(company, row.CompanyId));
        }
        Assert.DoesNotContain(db.ChangeTracker.Entries(), x => x.State is EntityState.Modified or EntityState.Added or EntityState.Deleted);
    }
}
