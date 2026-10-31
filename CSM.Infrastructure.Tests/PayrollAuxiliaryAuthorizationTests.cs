using System.Security.Claims;
using CSM.Api.Controllers;
using CSM.Domain.Entities.Organization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
        Assert.Equal(2400m, (await new SalaryStructureService(db).GetByEmployeeAsync(actor.Id, employee.Id, CancellationToken.None))!.BasicSalary);
        await Invoke(db, actor.Id, employee.Id, "calculate");
        await Invoke(db, actor.Id, employee.Id, "write");
        Assert.Equal(4800m, db.SalaryStructures.Single().BasicSalary);
    }

    [Theory]
    [InlineData("deleted-user")] [InlineData("deleted-role")] [InlineData("deleted-membership")]
    [InlineData("missing-user")] [InlineData("null-company")]
    public async Task Invalid_live_identity_cannot_read_or_write(string invalid)
    {
        using var db = Database();
        var actor = Seed(db, "HROfficer");
        if (invalid == "deleted-user") actor.IsDeleted = true;
        if (invalid == "deleted-role") actor.UserRoles.Single().Role.IsDeleted = true;
        if (invalid == "deleted-membership") actor.UserRoles.Single().IsDeleted = true;
        if (invalid == "null-company") actor.CompanyId = null;
        await db.SaveChangesAsync(true);
        var id = invalid == "missing-user" ? Guid.NewGuid() : actor.Id;
        foreach (var operation in new[] { "get", "write", "calculate" })
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Invoke(db, id, db.Employees.Single().Id, operation));
    }

    [Theory]
    [InlineData("get")] [InlineData("write")] [InlineData("calculate")]
    public async Task Missing_employee_parent_is_not_found_before_orphan_salary_access(string operation)
    {
        using var db = Database();
        var actor = Seed(db, "CompanyAdmin");
        var orphan = Guid.NewGuid();
        db.SalaryStructures.Add(new SalaryStructure { EmployeeId = orphan, BasicSalary = 1m, IsActive = true });
        await db.SaveChangesAsync(true);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Invoke(db, actor.Id, orphan, operation));
        Assert.Equal(1m, db.SalaryStructures.Single(x => x.EmployeeId == orphan).BasicSalary);
    }

    [Fact]
    public async Task Creating_salary_requires_authorized_employee_parent()
    {
        using var db = Database();
        var actor = Seed(db, "CompanyAdmin");
        var employee = db.Employees.Single();
        db.SalaryStructures.RemoveRange(db.SalaryStructures);
        var other = new Employee { CompanyId = Guid.NewGuid(), EmployeeNumber = "FOREIGN" };
        db.Employees.Add(other); await db.SaveChangesAsync(true);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Invoke(db, actor.Id, other.Id, "write"));
        Assert.Empty(db.SalaryStructures);
        await Invoke(db, actor.Id, employee.Id, "write");
        Assert.Equal(employee.Id, db.SalaryStructures.Single().EmployeeId);
        Assert.Equal(4800m, db.SalaryStructures.Single().BasicSalary);
    }

    [Fact]
    public async Task Global_admin_also_requires_current_view_grant()
    {
        using var db = Database();
        var actor = Seed(db, "SuperAdmin");
        db.RoleGranularPermissions.RemoveRange(db.RoleGranularPermissions);
        await db.SaveChangesAsync(true);
        foreach (var operation in new[] { "get", "write", "calculate" })
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Invoke(db, actor.Id, db.Employees.Single().Id, operation));
    }

    [Fact]
    public async Task Missing_salary_is_not_found_for_authorized_employee()
    {
        using var db = Database();
        var actor = Seed(db, "CompanyAdmin");
        db.SalaryStructures.RemoveRange(db.SalaryStructures);
        await db.SaveChangesAsync(true);
        var employee = db.Employees.Single();
        Assert.Null(await new SalaryStructureService(db).GetByEmployeeAsync(actor.Id, employee.Id, CancellationToken.None));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Invoke(db, actor.Id, employee.Id, "calculate"));
    }

    [Fact]
    public async Task Legacy_administrator_can_read_with_live_view_grant()
    {
        using var db = Database();
        var actor = Seed(db, "Administrator");
        var salary = await new SalaryStructureService(db).GetByEmployeeAsync(actor.Id, db.Employees.Single().Id, CancellationToken.None);
        Assert.Equal(2400m, salary!.BasicSalary);
    }

    [Theory]
    [InlineData("get")] [InlineData("write")] [InlineData("calculate")]
    public async Task Controllers_return_safe_forbidden_and_not_found(string operation)
    {
        using var db = Database();
        var actor = Seed(db, "HROfficer");
        var employee = db.Employees.Single();
        employee.CompanyId = Guid.NewGuid();
        await db.SaveChangesAsync(true);
        var context = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, actor.Id.ToString())], "test")) } };
        var salary = new SalaryStructuresController(new SalaryStructureService(db)) { ControllerContext = context };
        var calculations = new PayrollCalculationsController(new PayrollCalculationService(db)) { ControllerContext = context };
        async Task<IActionResult?> Request() => operation == "get" ? (await salary.Get(employee.Id, CancellationToken.None)).Result
            : operation == "write" ? (await salary.CreateOrUpdate(new UpdateSalaryStructureRequest { EmployeeId = employee.Id }, CancellationToken.None)).Result
            : (await calculations.Calculate(new CalculatePayrollRequest { EmployeeId = employee.Id }, CancellationToken.None)).Result;
        Assert.IsType<NotFoundResult>(await Request());
        actor.IsActive = false;
        await db.SaveChangesAsync(true);
        Assert.IsType<ForbidResult>(await Request());
        context.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "invalid")], "test"));
        Assert.IsType<ForbidResult>(await Request());
    }

    [Fact]
    public async Task PostgreSQL_enforces_company_and_live_grants_and_preserves_calculation_math()
    {
        var connection = new Npgsql.NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("CSM_TEST_POSTGRES")
            ?? "Host=localhost;Username=postgres;Password=postgres") { Database = "payroll_aux_" + Guid.NewGuid().ToString("N") };
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection.ConnectionString).Options);
        try
        {
            await db.Database.MigrateAsync();
            var own = new Company { Code = "OWN", Name = "Own" };
            var foreign = new Company { Code = "FOREIGN", Name = "Foreign" };
            User Actor(string role, Guid? company) => new() { CompanyId = company, Email = role + "@example.test", NormalizedEmail = role.ToUpperInvariant() + "@EXAMPLE.TEST",
                UserRoles = [new UserRole { Role = new Role { Name = role, NormalizedName = role.ToUpperInvariant(),
                    GranularPermissions = [new RoleGranularPermission { Permission = GranularPermission.HRM_Payroll_View }] } }] };
            var hr = Actor("HROfficer", own.Id); var ca = Actor("CompanyAdmin", own.Id); var sa = Actor("SuperAdmin", null);
            var employee = new Employee { CompanyId = own.Id, EmployeeNumber = "OWN-1" };
            var other = new Employee { CompanyId = foreign.Id, EmployeeNumber = "FOREIGN-1" };
            SalaryStructure Salary(Employee e) => new() { EmployeeId = e.Id, BasicSalary = 2400m, HousingAllowance = 100m,
                TransportAllowance = 200m, PositionAllowance = 300m, OvertimeMultiplier = 1.5m, PensionPercentage = 7m,
                BankName = "Private", BankAccountNumber = "private-account", IsActive = true };
            db.Companies.AddRange(own, foreign); db.Users.AddRange(hr, ca, sa); db.Employees.AddRange(employee, other);
            db.SalaryStructures.AddRange(Salary(employee), Salary(other));
            await db.SaveChangesAsync();
            foreach (var actor in new[] { hr, ca })
            {
                foreach (var operation in new[] { "get", "write", "calculate" })
                    await Assert.ThrowsAsync<KeyNotFoundException>(() => Invoke(db, actor.Id, other.Id, operation));
                var calculation = await new PayrollCalculationService(db).CalculateAsync(actor.Id,
                    new CalculatePayrollRequest { EmployeeId = employee.Id, OvertimeHours = 8m, TaxDeduction = 50m, OtherDeduction = 20m }, CancellationToken.None);
                Assert.Equal(120m, calculation.OvertimeAmount); Assert.Equal(3120m, calculation.GrossSalary);
                Assert.Equal(218.4m, calculation.PensionDeduction); Assert.Equal(2831.6m, calculation.NetSalary);
            }
            var overridden = await new PayrollCalculationService(db).CalculateAsync(hr.Id,
                new CalculatePayrollRequest { EmployeeId = employee.Id, OvertimeHours = 8m, PensionDeduction = 10m, TaxDeduction = 50m, OtherDeduction = 20m }, CancellationToken.None);
            Assert.Equal(3040m, overridden.NetSalary);
            Assert.Equal("private-account", (await new SalaryStructureService(db).GetByEmployeeAsync(sa.Id, other.Id, CancellationToken.None))!.BankAccountNumber);
            await Invoke(db, hr.Id, employee.Id, "write");
            await Invoke(db, sa.Id, other.Id, "write");
            Assert.All(await db.SalaryStructures.ToListAsync(), x => Assert.Equal(4800m, x.BasicSalary));
            var grant = await db.RoleGranularPermissions.SingleAsync(x => x.RoleId == hr.UserRoles.Single().RoleId);
            db.RoleGranularPermissions.Remove(grant); await db.SaveChangesAsync();
            foreach (var operation in new[] { "get", "write", "calculate" })
                await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Invoke(db, hr.Id, employee.Id, operation));
            sa.IsActive = false; await db.SaveChangesAsync();
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Invoke(db, sa.Id, other.Id, "get"));
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private static ApplicationDbContext Database() => new TestDb();

    private sealed class TestDb : ApplicationDbContext
    {
        public TestDb() : base(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options) { }
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => base.SaveChangesAsync(true, cancellationToken);
    }

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

    private static Task Invoke(ApplicationDbContext db, Guid actor, Guid employee, string operation) => operation switch
    {
        "get" => new SalaryStructureService(db).GetByEmployeeAsync(actor, employee, CancellationToken.None),
        "write" => new SalaryStructureService(db).CreateOrUpdateAsync(actor,
            new UpdateSalaryStructureRequest { EmployeeId = employee, BasicSalary = 4800m }, CancellationToken.None),
        _ => new PayrollCalculationService(db).CalculateAsync(actor,
            new CalculatePayrollRequest { EmployeeId = employee }, CancellationToken.None)
    };
}
