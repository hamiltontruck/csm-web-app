using System.Text.Json;
using CSM.Application.Common.Exceptions;
using CSM.Application.Finance.Budgets.Dtos;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Security;
using CSM.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class CompanyBudgetCreationTests
{
    [Fact]
    public async Task Companyless_superadmin_can_create_budget_for_selected_company()
    {
        using var db = CreateDatabase(true);
        var result = await new BudgetService(db, new Permissions()).CreateAsync(ActorId, Request(OtherCompany));
        Assert.Equal(OtherCompany, result.CompanyId);
        Assert.Equal(100000m, result.TotalAmount);
        Assert.Equal(OtherCompany, db.Budgets.Single().CompanyId);
    }

    [Fact]
    public async Task Company_user_cannot_create_budget_for_another_company()
    {
        using var db = CreateDatabase(false);
        await Assert.ThrowsAsync<BudgetManagementException>(() =>
            new BudgetService(db, new Permissions()).CreateAsync(ActorId, Request(OtherCompany)));
        Assert.Empty(db.Budgets);
    }

    [Fact]
    public async Task Company_user_without_selection_uses_assigned_company()
    {
        using var db = CreateDatabase(false);
        var result = await new BudgetService(db, new Permissions()).CreateAsync(ActorId, Request(null));
        Assert.Equal(OwnCompany, result.CompanyId);
    }

    [Fact]
    public async Task Unknown_company_is_rejected()
    {
        using var db = CreateDatabase(true);
        await Assert.ThrowsAsync<BudgetManagementException>(() =>
            new BudgetService(db, new Permissions()).CreateAsync(ActorId, Request(Guid.NewGuid())));
        Assert.Empty(db.Budgets);
    }

    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly Guid OwnCompany = Guid.NewGuid();
    private static readonly Guid OtherCompany = Guid.NewGuid();

    private static CreateBudgetRequest Request(Guid? companyId) =>
        JsonSerializer.Deserialize<CreateBudgetRequest>(JsonSerializer.Serialize(new
        {
            CompanyId = companyId, BudgetNumber = "HBG-BUD-001", Name = "Test Budget",
            TotalAmount = 100000m, CurrencyCode = "ETB",
            EffectiveFrom = new DateOnly(2026, 10, 18), EffectiveTo = new DateOnly(2026, 12, 31)
        }))!;

    private static TestDatabase CreateDatabase(bool superAdmin)
    {
        var db = new TestDatabase(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Companies.AddRange(new Company { Id = OwnCompany, Name = "Own", Code = "OWN" },
            new Company { Id = OtherCompany, Name = "Other", Code = "OTHER" });
        db.Users.Add(new User
        {
            Id = ActorId, CompanyId = superAdmin ? null : OwnCompany,
            UserRoles = [new UserRole { Id = Guid.NewGuid(), Role = new Role
            {
                Id = Guid.NewGuid(), Name = superAdmin ? AppRoles.SuperAdmin : AppRoles.CompanyAdmin
            }}]
        });
        db.SaveChanges(acceptAllChangesOnSuccess: true);
        return db;
    }

    private sealed class TestDatabase(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
    {
        // Exercise budget persistence using InMemory without PostgreSQL timestamp normalization.
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => base.SaveChangesAsync(acceptAllChangesOnSuccess: true, cancellationToken);
    }

    private sealed class Permissions : IPermissionEvaluator
    {
        public Task<bool> HasPermissionAsync(Guid id, string permission, CancellationToken token = default) => Task.FromResult(true);
        public Task<IReadOnlyCollection<string>> GetPermissionsAsync(Guid id, CancellationToken token = default) =>
            Task.FromResult<IReadOnlyCollection<string>>([]);
    }
}
