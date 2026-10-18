using CSM.Application.Common.Exceptions;
using CSM.Application.Finance.Expenses.Dtos;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Finance;
using CSM.Domain.Entities.Identity;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class ExpenseCompanyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Superadmin_uses_selected_cost_code_company(bool assignedElsewhere)
    {
        using var db = Database(true, assignedElsewhere ? OwnCompany : null);
        var result = await new ExpenseService(db).CreateAsync(ActorId, Request());
        Assert.Equal(CodeCompany, result.CompanyId);
        Assert.Equal(CodeCompany, db.Expenses.Single().CompanyId);
    }

    [Fact]
    public async Task Ordinary_user_cannot_use_other_company_cost_code()
    {
        using var db = Database(false, OwnCompany);
        await Assert.ThrowsAsync<ExpenseManagementException>(() =>
            new ExpenseService(db).CreateAsync(ActorId, Request()));
        Assert.Empty(db.Expenses);
    }

    [Fact]
    public async Task Ordinary_user_can_submit_own_company_expense()
    {
        using var db = Database(false, CodeCompany);
        var result = await new ExpenseService(db).CreateAsync(ActorId, Request());
        Assert.Equal(CodeCompany, result.CompanyId);
    }

    [Fact]
    public async Task Unknown_cost_code_is_rejected()
    {
        using var db = Database(true, null);
        await Assert.ThrowsAsync<ExpenseManagementException>(() =>
            new ExpenseService(db).CreateAsync(ActorId, Request() with { CostCodeId = Guid.NewGuid() }));
        Assert.Empty(db.Expenses);
    }

    [Fact]
    public async Task Vendor_from_other_company_is_rejected_for_superadmin()
    {
        using var db = Database(true, null);
        var vendor = new Vendor { Id = Guid.NewGuid(), CompanyId = OwnCompany, VendorCode = "OTHER", Name = "Other" };
        db.Vendors.Add(vendor);
        db.SaveChanges(acceptAllChangesOnSuccess: true);
        await Assert.ThrowsAsync<ExpenseManagementException>(() =>
            new ExpenseService(db).CreateAsync(ActorId, Request() with { VendorId = vendor.Id }));
        Assert.Empty(db.Expenses);
    }

    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly Guid CodeId = Guid.NewGuid();
    private static readonly Guid OwnCompany = Guid.NewGuid();
    private static readonly Guid CodeCompany = Guid.NewGuid();

    private static CreateExpenseRequest Request() =>
        new(null, null, CodeId, null, "EXP-001", new DateOnly(2026, 10, 18),
            "Construction expense", 1000m, 0m, "ETB", 1m, null, null);

    private static TestDatabase Database(bool admin, Guid? actorCompany)
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
        db.CostCodes.Add(new CostCode { Id = CodeId, CompanyId = CodeCompany, Code = "CODE-001", Name = "Construction" });
        db.SaveChanges(acceptAllChangesOnSuccess: true);
        return db;
    }

    private sealed class TestDatabase(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
    {
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            base.SaveChangesAsync(acceptAllChangesOnSuccess: true, cancellationToken);
    }
}
