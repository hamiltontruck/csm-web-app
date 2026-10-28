using System.Reflection;
using System.Security.Claims;
using CSM.Api.Controllers;
using CSM.Application.Common.Security;
using CSM.Domain.Entities.HRM;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class AttendanceAccessTests
{
    [Theory]
    [InlineData("SuperAdmin", true, true)]
    [InlineData("CompanyAdmin", true, true)]
    [InlineData("SiteManager", true, true)]
    [InlineData("Supervisor", true, false)]
    [InlineData("HrOfficer", true, false)]
    [InlineData("HROfficer", true, false)]
    [InlineData("Administrator", false, false)]
    [InlineData("Storekeeper", false, false)]
    [InlineData("Employee", false, false)]
    public async Task Endpoint_roles_match_existing_service_read_record_and_review_contract(string role, bool record, bool review)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddAuthorization(o => o.AddPolicy(AppPolicies.HRManagement, p => p.RequireRole(AppPolicies.HRManagementRoles)));
        using var provider = services.BuildServiceProvider();
        var actor = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "test"));
        foreach (var name in new[] { "GetAll", "GetById", "CheckIn", "CheckOut", "Approve", "Override" })
        {
            var metadata = typeof(AttendancesController).GetCustomAttributes(true).OfType<IAuthorizeData>()
                .Concat(typeof(AttendancesController).GetMethod(name)!.GetCustomAttributes(true).OfType<IAuthorizeData>());
            var policy = await AuthorizationPolicy.CombineAsync(provider.GetRequiredService<IAuthorizationPolicyProvider>(), metadata);
            Assert.Equal(name is "Approve" or "Override" ? review : record,
                (await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(actor, null, policy!)).Succeeded);
        }
    }

    [Fact]
    public async Task Company_admin_reads_only_own_company_and_foreign_record_returns_403()
    {
        using var db = Database("CompanyAdmin", out var actor, out var own, out var foreign);
        var controller = Controller(db, actor);
        var result = await controller.GetAll(null, null, null, null, default);
        var rows = Assert.IsAssignableFrom<IReadOnlyCollection<CSM.Application.HRM.Attendances.Dtos.AttendanceResponse>>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(own, Assert.Single(rows).Id);
        Assert.Equal(403, Assert.IsType<ObjectResult>((await controller.GetById(foreign, default)).Result).StatusCode);
        Assert.Equal(404, Assert.IsType<NotFoundObjectResult>((await controller.GetById(Guid.NewGuid(), default)).Result).StatusCode);
        Assert.IsType<BadRequestObjectResult>((await controller.GetAll(null, null, new(2026,10,29), new(2026,10,28), default)).Result);
    }

    [Theory]
    [InlineData("Supervisor")]
    [InlineData("HrOfficer")]
    [InlineData("HROfficer")]
    public async Task Recorders_cannot_approve_or_override_and_record_is_unchanged(string role)
    {
        using var db = Database(role, out var actor, out var own, out _);
        var controller = Controller(db, actor);
        Assert.Equal(403, Assert.IsType<ObjectResult>((await controller.Approve(own, new(null), default)).Result).StatusCode);
        Assert.Equal(403, Assert.IsType<ObjectResult>((await controller.Override(own, new(null, null, CSM.Domain.Enums.AttendanceStatus.Present, "Test", null), default)).Result).StatusCode);
        Assert.False(db.Attendances.Single(x => x.Id == own).IsApproved);
        Assert.DoesNotContain(db.ChangeTracker.Entries(), x => x.State is EntityState.Modified or EntityState.Added or EntityState.Deleted);
    }

    private static ApplicationDbContext Database(string role, out Guid actor, out Guid own, out Guid foreign)
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        actor = Guid.NewGuid(); var company = Guid.NewGuid(); own = Guid.NewGuid(); foreign = Guid.NewGuid();
        db.Users.Add(new User { Id = actor, CompanyId = company, IsActive = true, UserRoles = [
            new UserRole { Id = Guid.NewGuid(), Role = new Role { Id = Guid.NewGuid(), Name = role } }] });
        foreach (var pair in new[] { (own, company), (foreign, Guid.NewGuid()) })
            db.Attendances.Add(new Attendance { Id = pair.Item1, CompanyId = pair.Item2, AttendanceDate = new(2026,10,28),
                Employee = new Employee { Id = Guid.NewGuid(), CompanyId = pair.Item2, EmployeeNumber = pair.Item1.ToString(), FirstName = "Test", LastName = "Employee" },
                ConstructionSite = new ConstructionSite { Id = Guid.NewGuid(), CompanyId = pair.Item2, SiteCode = pair.Item1.ToString(), Name = "Test Site" } });
        db.SaveChanges(true); return db;
    }
    private static AttendancesController Controller(ApplicationDbContext db, Guid actor) =>
        new(new CSM.Infrastructure.Services.AttendanceService(db)) { ControllerContext = new ControllerContext {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, actor.ToString())], "test")) } } };
}
