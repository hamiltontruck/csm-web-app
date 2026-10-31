using System.Reflection;
using CSM.Application.Payroll.Dtos;
using CSM.Application.Payroll.Calculations.Dtos;
using CSM.Domain.Entities.HRM;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class PayrollAuxiliaryAuthorizationTests
{
    [Theory]
    [InlineData("get")] [InlineData("write")] [InlineData("calculate")]
    public async Task Foreign_employee_is_hidden_and_salary_is_unchanged(string operation)
    {
        using var db = Database();
        var actor = Seed(db, "HROfficer");
        var employee = db.Employees.Single();
        employee.CompanyId = Guid.NewGuid();
        await db.SaveChangesAsync(true);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Invoke(db, actor.Id, employee.Id, operation));
        Assert.Equal(2400m, db.SalaryStructures.Single().BasicSalary);
    }

    [Theory]
    [InlineData("get")] [InlineData("write")] [InlineData("calculate")]
    public async Task Revoked_live_grant_denies_even_with_previously_loaded_actor(string operation)
    {
        using var db = Database();
        var actor = Seed(db, "HROfficer");
        db.RoleGranularPermissions.RemoveRange(db.RoleGranularPermissions);
        await db.SaveChangesAsync(true);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Invoke(db, actor.Id, db.Employees.Single().Id, operation));
    }

    [Theory]
    [InlineData("get")] [InlineData("write")] [InlineData("calculate")]
    public async Task Inactive_actor_denied(string operation)
    {
        using var db = Database();
        var actor = Seed(db, "SuperAdmin");
        actor.IsActive = false;
        await db.SaveChangesAsync(true);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Invoke(db, actor.Id, db.Employees.Single().Id, operation));
    }

    [Theory]
    [InlineData("SiteManager")] [InlineData("Supervisor")] [InlineData("Accountant")]
    public async Task Other_payroll_reader_roles_cannot_read_auxiliary_salary(string role)
    {
        using var db = Database();
        var actor = Seed(db, role);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Invoke(db, actor.Id, db.Employees.Single().Id, "get"));
    }

    [Theory]
    [InlineData("get")] [InlineData("write")] [InlineData("calculate")]
    public async Task Empty_company_fails_closed(string operation)
    {
        using var db = Database();
        var actor = Seed(db, "CompanyAdmin");
        actor.CompanyId = Guid.Empty;
        db.Employees.Single().CompanyId = Guid.Empty;
        await db.SaveChangesAsync(true);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Invoke(db, actor.Id, db.Employees.Single().Id, operation));
    }

    [Theory]
    [InlineData("Administrator", "write")] [InlineData("Administrator", "calculate")]
    public async Task Legacy_administrator_is_read_only(string role, string operation)
    {
        using var db = Database();
        var actor = Seed(db, role);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Invoke(db, actor.Id, db.Employees.Single().Id, operation));
    }

    [Theory]
    [InlineData("SuperAdmin")] [InlineData("CompanyAdmin")] [InlineData("HrOfficer")] [InlineData("HROfficer")]
    public async Task Managers_with_current_grant_read_write_and_calculate(string role)
    {
        using var db = Database();
        var actor = Seed(db, role);
        var employee = db.Employees.Single();
        if (role == "SuperAdmin") { actor.CompanyId = null; employee.CompanyId = Guid.NewGuid(); await db.SaveChangesAsync(true); }
        await Invoke(db, actor.Id, employee.Id, "get");
        await Invoke(db, actor.Id, employee.Id, "calculate");
        await Invoke(db, actor.Id, employee.Id, "write");
        Assert.Equal(4800m, db.SalaryStructures.Single().BasicSalary);
    }

    private static ApplicationDbContext Database() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static User Seed(ApplicationDbContext db, string role)
    {
        var company = Guid.NewGuid();
        var actor = new User { CompanyId = company, IsActive = true, UserRoles = [new UserRole { Role = new Role {
            Name = role, GranularPermissions = [new RoleGranularPermission { Permission = GranularPermission.HRM_Payroll_View }] } }] };
        var employee = new Employee { CompanyId = company, EmployeeNumber = "PAY-1" };
        db.Users.Add(actor);
        db.Employees.Add(employee);
        db.SalaryStructures.Add(new SalaryStructure { EmployeeId = employee.Id, BasicSalary = 2400m, IsActive = true });
        db.SaveChanges(true);
        return actor;
    }

    // Supports the vulnerable pre-fix signature so the baseline fails on behavior, not compilation.
    private static async Task Invoke(ApplicationDbContext db, Guid actor, Guid employee, string operation)
    {
        object service = operation == "calculate" ? new PayrollCalculationService(db) : new SalaryStructureService(db);
        var method = service.GetType().GetMethod(operation == "get" ? "GetByEmployeeAsync" : operation == "write" ? "CreateOrUpdateAsync" : "CalculateAsync")!;
        object argument = operation == "get" ? employee : operation == "write"
            ? new UpdateSalaryStructureRequest { EmployeeId = employee, BasicSalary = 4800m }
            : new CalculatePayrollRequest { EmployeeId = employee };
        object[] args = method.GetParameters().Length == 3 ? [actor, argument, CancellationToken.None] : [argument, CancellationToken.None];
        try { await (Task)method.Invoke(service, args)!; }
        catch (TargetInvocationException ex) when (ex.InnerException is not null) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw(); }
    }
}
