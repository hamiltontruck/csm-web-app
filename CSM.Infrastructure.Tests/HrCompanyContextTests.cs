using System.Text.Json;
using CSM.Application.HRM.Shifts.Dtos;
using CSM.Domain.Entities.HRM;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Security;
using CSM.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class HrCompanyContextTests
{
    [Theory]
    [InlineData("leave")]
    [InlineData("assignment")]
    [InlineData("attendance")]
    [InlineData("shift")]
    [InlineData("contractor")]
    public async Task Independently_granted_companyless_superadmin_uses_selected_resource_company(string flow)
    {
        using var db = Database(out var actor, out var company, out var employee, out var site);
        var day = new DateOnly(2026, 11, 1);
        Guid actual;
        switch (flow)
        {
            case "leave":
                actual = (await new LeaveService(db).CreateAsync(actor, new(employee, "Annual", day, day, null))).CompanyId;
                break;
            case "assignment":
                actual = (await new SiteAssignmentService(db).CreateAsync(actor, new(employee, site, day.AddDays(1), day.AddDays(1), null, false, null))).CompanyId;
                break;
            case "attendance":
                actual = (await new AttendanceService(db).CheckInAsync(actor, new(employee, site, null, day.ToDateTime(new TimeOnly(8, 0), DateTimeKind.Utc), null, null, null, AttendanceSource.Manual, null, null))).CompanyId;
                break;
            case "shift":
                var shift = JsonSerializer.Deserialize<CreateShiftRequest>(JsonSerializer.Serialize(new { Code = "DAY", Name = "Day", StartTime = new TimeOnly(8,0), EndTime = new TimeOnly(16,0), GracePeriodMinutes = 0, StandardHours = 8m, IsNightShift = false, CompanyId = company }))!;
                actual = (await new ShiftService(db).CreateAsync(actor, shift)).CompanyId;
                break;
            default:
                var contractor = JsonSerializer.Deserialize<CSM.Application.HRM.Contractors.Dtos.CreateContractorRequest>(JsonSerializer.Serialize(new { ContractorCode = "C1", Name = "Contractor", CompanyId = company }))!;
                actual = (await new ContractorService(db, new PermissionEvaluator(db)).CreateAsync(actor, contractor)).CompanyId;
                break;
        }
        Assert.Equal(company, actual);
        Assert.Null(db.Users.Single(x => x.Id == actor).CompanyId);
    }

    [Theory]
    [InlineData("leave")]
    [InlineData("timesheet")]
    public async Task Independently_granted_companyless_superadmin_employee_options_span_companies(string flow)
    {
        using var db = Database(out var actor, out _, out _, out _);
        db.Employees.Add(new Employee { Id = Guid.NewGuid(), CompanyId = Guid.NewGuid(), EmployeeNumber = "OTHER", FirstName = "Other", LastName = "Employee", Status = EmployeeStatus.Active });
        await db.SaveChangesAsync();
        var count = flow == "leave" ? (await new LeaveService(db).GetEmployeeOptionsAsync(actor)).Count : (await new TimesheetService(db).GetEmployeeOptionsAsync(actor)).Count;
        Assert.Equal(2, count);
    }

    [Theory]
    [InlineData("leave")]
    [InlineData("assignment")]
    [InlineData("attendance")]
    public async Task Ungranted_superadmin_cannot_borrow_other_role_grants_to_cross_company(string flow)
    {
        using var db = Database(out var actor, out _, out var employee, out var site);
        db.Users.Single(x => x.Id == actor).CompanyId = Guid.NewGuid();
        var role = db.Roles.Single(x => x.Name == "SuperAdmin");
        db.RoleGranularPermissions.RemoveRange(db.RoleGranularPermissions.Where(x => x.RoleId == role.Id));
        db.UserRoles.Add(new UserRole { UserId = actor, Role = new Role { Name = "HrOfficer", GranularPermissions = Enum.GetValues<GranularPermission>().Select(p => new RoleGranularPermission { Permission = p }).ToList() } });
        await db.SaveChangesAsync();
        await Assert.ThrowsAnyAsync<Exception>(async () => {
            if (flow == "leave") await new LeaveService(db).CreateAsync(actor, new(employee, "Annual", new(2026,12,1), new(2026,12,1), null));
            else if (flow == "assignment") await new SiteAssignmentService(db).CreateAsync(actor, new(employee, site, new(2026,12,1), null, null, false, null));
            else await new AttendanceService(db).CheckInAsync(actor, new(employee, site, null, new(2026,11,1,8,0,0,DateTimeKind.Utc), null, null, null, AttendanceSource.Manual, null, null));
        });
        Assert.Empty(db.LeaveRequests);
        Assert.Empty(db.Attendances);
        Assert.Single(db.SiteAssignments);
    }

    [Theory]
    [InlineData("assignment")]
    [InlineData("attendance")]
    public async Task Global_authority_preserves_employee_site_company_consistency(string flow)
    {
        using var db = Database(out var actor, out _, out var employee, out var site);
        db.ConstructionSites.Single(x => x.Id == site).CompanyId = Guid.NewGuid();
        await db.SaveChangesAsync();
        var error = await Assert.ThrowsAnyAsync<Exception>(async () => {
            if (flow == "assignment") await new SiteAssignmentService(db).CreateAsync(actor, new(employee, site, new(2026,12,1), null, null, false, null));
            else await new AttendanceService(db).CheckInAsync(actor, new(employee, site, null, new(2026,11,1,8,0,0,DateTimeKind.Utc), null, null, null, AttendanceSource.Manual, null, null));
        });
        Assert.Equal("Construction site does not belong to your company.", error.Message);
        Assert.Empty(db.Attendances);
        Assert.Single(db.SiteAssignments);
    }

    [Theory]
    [InlineData("shift")]
    [InlineData("contractor")]
    public async Task Action_options_only_offer_active_companies_and_removed_independent_grant_removes_global_authority(string flow)
    {
        using var db = Database(out var actor, out var company, out _, out _);
        db.Companies.Add(new Company { Id = Guid.NewGuid(), Code = "INACTIVE", Name = "Inactive", IsActive = false });
        db.Companies.Add(new Company { Id = Guid.NewGuid(), Code = "DELETED", Name = "Deleted", IsActive = true, IsDeleted = true });
        await db.SaveChangesAsync();
        var options = flow == "shift" ? await new ShiftService(db).GetCompanyOptionsAsync(actor)
            : await new ContractorService(db, new PermissionEvaluator(db)).GetCompanyOptionsAsync(actor);
        Assert.True(options.CanSelectCompany);
        Assert.Equal(company, Assert.Single(options.Companies).Id);
        var permission = flow == "shift" ? GranularPermission.HRM_Shift_Submit : GranularPermission.HRM_Contractor_Manage;
        db.RoleGranularPermissions.Remove(db.RoleGranularPermissions.Single(x => x.Permission == permission));
        await db.SaveChangesAsync();
        await Assert.ThrowsAnyAsync<Exception>(async () => {
            if (flow == "shift") await new ShiftService(db).GetCompanyOptionsAsync(actor);
            else await new ContractorService(db, new PermissionEvaluator(db)).GetCompanyOptionsAsync(actor);
        });
    }

    [Theory]
    [InlineData("shift", "missing")]
    [InlineData("shift", "inactive")]
    [InlineData("contractor", "missing")]
    [InlineData("contractor", "inactive")]
    public async Task Global_catalog_creation_requires_valid_selected_company(string flow, string selection)
    {
        using var db = Database(out var actor, out var company, out _, out _);
        db.Companies.Single(x => x.Id == company).IsActive = false;
        await db.SaveChangesAsync();
        Guid? selected = selection == "missing" ? null : company;
        await Assert.ThrowsAnyAsync<Exception>(async () => {
            if (flow == "shift") await new ShiftService(db).CreateAsync(actor, new("DAY", "Day", new(8,0), new(16,0), 0, 8m, false, selected));
            else await new ContractorService(db, new PermissionEvaluator(db)).CreateAsync(actor, new() { ContractorCode = "C", Name = "Contractor", CompanyId = selected });
        });
        Assert.Empty(db.Shifts);
        Assert.Empty(db.Contractors);
    }

    [Fact]
    public async Task Role_only_timesheet_lookup_rejects_deleted_global_membership()
    {
        using var db = Database(out var actor, out _, out _, out _);
        db.UserRoles.Single(x => x.UserId == actor).IsDeleted = true;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<CSM.Application.Common.Exceptions.TimesheetManagementException>(() => new TimesheetService(db).GetEmployeeOptionsAsync(actor));
    }

    [Fact]
    public async Task Assigned_company_normal_shift_creation_still_omits_selection_and_cannot_inject_another_company()
    {
        using var db = Database(out var actor, out var company, out _, out _);
        db.Users.Single(x => x.Id == actor).CompanyId = company;
        db.Roles.Single(x => x.Name == "SuperAdmin").Name = "CompanyAdmin";
        await db.SaveChangesAsync();
        var service = new ShiftService(db);
        Assert.False((await service.GetCompanyOptionsAsync(actor)).CanSelectCompany);
        Assert.Equal(company, (await service.CreateAsync(actor, new("OWN", "Own", new(8,0), new(16,0), 0, 8m, false))).CompanyId);
        await Assert.ThrowsAsync<CSM.Application.Common.Exceptions.ShiftManagementException>(() => service.CreateAsync(actor, new("OTHER", "Other", new(8,0), new(16,0), 0, 8m, false, Guid.NewGuid())));
        Assert.Single(db.Shifts);
    }

    [Theory]
    [InlineData(typeof(CSM.Api.Controllers.ShiftsController))]
    [InlineData(typeof(CSM.Api.Controllers.ContractorsController))]
    public void Company_options_route_has_exact_existing_create_action_requirements(Type controller)
    {
        static string[] Requirements(System.Reflection.MethodInfo method) => method.GetCustomAttributes(true)
            .OfType<Microsoft.AspNetCore.Authorization.IAuthorizeData>()
            .Select(x => $"{x.Policy}|{x.Roles}|{x.AuthenticationSchemes}").Order().ToArray();
        Assert.Equal(Requirements(controller.GetMethod("Create")!), Requirements(controller.GetMethod("GetCompanyOptions")!));
        Assert.Equal("company-options", ((Microsoft.AspNetCore.Mvc.HttpGetAttribute)controller.GetMethod("GetCompanyOptions")!
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.HttpGetAttribute), false).Single()).Template);
        // Contractor's class-level HRManagement gate is intentionally unchanged.
        if (controller == typeof(CSM.Api.Controllers.ContractorsController))
            Assert.Contains(controller.GetCustomAttributes(true).OfType<Microsoft.AspNetCore.Authorization.IAuthorizeData>(),
                x => x.Policy == CSM.Application.Common.Security.AppPolicies.HRManagement);
    }

    private static ApplicationDbContext Database(out Guid actor, out Guid company, out Guid employee, out Guid site)
    {
        var db = new MemoryContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        actor = Guid.NewGuid(); company = Guid.NewGuid(); employee = Guid.NewGuid(); site = Guid.NewGuid();
        var role = new Role { Id = Guid.NewGuid(), Name = "SuperAdmin", GranularPermissions = Enum.GetValues<GranularPermission>().Select(p => new RoleGranularPermission { Id = Guid.NewGuid(), Permission = p }).ToList() };
        db.Users.Add(new User { Id = actor, IsActive = true, UserRoles = [new UserRole { Id = Guid.NewGuid(), Role = role }] });
        db.Companies.Add(new Company { Id = company, Code = "COMP", Name = "Company", IsActive = true });
        db.Employees.Add(new Employee { Id = employee, CompanyId = company, EmployeeNumber = "EMP", FirstName = "Test", LastName = "Employee", Status = EmployeeStatus.Active });
        db.ConstructionSites.Add(new ConstructionSite { Id = site, CompanyId = company, SiteCode = "SITE", Name = "Site", IsActive = true });
        db.SiteAssignments.Add(new SiteAssignment { Id = Guid.NewGuid(), CompanyId = company, EmployeeId = employee, ConstructionSiteId = site, StartDate = new DateOnly(2026,11,1), EndDate = new DateOnly(2026,11,1) });
        db.SaveChanges();
        return db;
    }
    // Production timestamp normalization requires relational mappings; service tests use InMemory.
    private sealed class MemoryContext(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
    {
        public override int SaveChanges() => base.SaveChanges(true);
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => base.SaveChangesAsync(true, cancellationToken);
    }
}
