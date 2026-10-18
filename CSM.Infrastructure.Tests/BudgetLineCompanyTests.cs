using CSM.Application.Common.Exceptions;
using CSM.Application.Finance.BudgetLines.Dtos;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Finance;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class BudgetLineCompanyTests
{
    [Fact]
    public async Task Companyless_admin_creates_line_in_budget_company()
    {
        using var db = Database(true);
        var result = await new BudgetLineService(db).CreateAsync(ActorId, Request());
        Assert.Equal(BudgetCompany, result.CompanyId);
        Assert.Equal(BudgetCompany, db.BudgetLines.Single().CompanyId);
        Assert.Equal(100000m, result.BudgetedAmount);
    }

    [Fact]
    public async Task Admin_assigned_elsewhere_can_manage_selected_budget()
    {
        using var db = Database(true, OwnCompany);
        var result = await new BudgetLineService(db).CreateAsync(ActorId, Request());
        Assert.Equal(BudgetCompany, result.CompanyId);
    }

    [Fact]
    public async Task Company_user_can_create_line_in_own_budget()
    {
        using var db = Database(false, BudgetCompany);
        var result = await new BudgetLineService(db).CreateAsync(ActorId, Request());
        Assert.Equal(BudgetCompany, result.CompanyId);
    }

    [Fact]
    public async Task Company_user_cannot_create_line_in_other_company_budget()
    {
        using var db = Database(false, OwnCompany);
        await Assert.ThrowsAsync<BudgetLineManagementException>(() =>
            new BudgetLineService(db).CreateAsync(ActorId, Request()));
        Assert.Empty(db.BudgetLines);
    }

    [Fact]
    public async Task Admin_cannot_attach_cost_code_from_other_company()
    {
        using var db = Database(true);
        db.CostCodes.Single().CompanyId = OwnCompany;
        db.SaveChanges(acceptAllChangesOnSuccess: true);
        await Assert.ThrowsAsync<BudgetLineManagementException>(() =>
            new BudgetLineService(db).CreateAsync(ActorId, Request()));
        Assert.Empty(db.BudgetLines);
    }

    [Fact]
    public async Task Closed_budget_remains_uneditable_for_admin()
    {
        using var db = Database(true);
        db.Budgets.Single().Status = BudgetStatus.Closed;
        db.SaveChanges(acceptAllChangesOnSuccess: true);
        await Assert.ThrowsAsync<BudgetLineManagementException>(() =>
            new BudgetLineService(db).CreateAsync(ActorId, Request()));
        Assert.Empty(db.BudgetLines);
    }

    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly Guid BudgetId = Guid.NewGuid();
    private static readonly Guid CodeId = Guid.NewGuid();
    private static readonly Guid OwnCompany = Guid.NewGuid();
    private static readonly Guid BudgetCompany = Guid.NewGuid();

    private static CreateBudgetLineRequest Request() =>
        new(BudgetId, CodeId, "Construction", 100000m, 100000m);

    private static TestDatabase Database(bool admin, Guid? actorCompany = null)
    {
        var db = new TestDatabase(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Users.Add(new User
        {
            Id = ActorId, CompanyId = actorCompany,
            UserRoles = [new UserRole { Id = Guid.NewGuid(), Role = new Role
            {
                Id = Guid.NewGuid(), Name = admin ? AppRoles.SuperAdmin : AppRoles.CompanyAdmin
            }}]
        });
        db.Budgets.Add(new Budget
        {
            Id = BudgetId, CompanyId = BudgetCompany, BudgetNumber = "BUD-001",
            Name = "Construction Budget", TotalAmount = 100000m, Status = BudgetStatus.Approved
        });
        db.CostCodes.Add(new CostCode
        {
            Id = CodeId, CompanyId = BudgetCompany, Code = "COST-001", Name = "Construction"
        });
        db.SaveChanges(acceptAllChangesOnSuccess: true);
        return db;
    }

    private sealed class TestDatabase(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
    {
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            base.SaveChangesAsync(acceptAllChangesOnSuccess: true, cancellationToken);
    }
}
