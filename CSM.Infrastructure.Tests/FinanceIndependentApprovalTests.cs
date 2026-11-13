using CSM.Application.Common.Exceptions;
using CSM.Application.Finance.Budgets.Dtos;
using CSM.Application.Finance.BudgetLines.Dtos;
using CSM.Application.Finance.Expenses.Dtos;
using CSM.Domain.Entities.Finance;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Enums;
using CSM.Infrastructure.Security;
using CSM.Infrastructure.Services;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class FinanceIndependentApprovalTests
{
    [Theory]
    [InlineData("budget")] [InlineData("expense")]
    public async Task New_record_authorship_is_server_owned(string kind)
    {
        using var f = new Fixture();
        if (kind == "budget")
        {
            var row = await new BudgetService(f.Db, new PermissionEvaluator(f.Db)).CreateAsync(f.Actor.Id,
                new(f.Site.Id, null, "NEW", "New budget", null, 100m, "ETB", new DateOnly(2026,11,13), null));
            var saved = f.Db.Budgets.Single(x => x.Id == row.Id);
            Assert.Equal(f.Actor.Id, saved.CreatedBy); Assert.Equal(f.Actor.Id, saved.UpdatedBy);
        }
        else
        {
            var row = await new ExpenseService(f.Db).CreateAsync(f.Actor.Id,
                new(f.Site.Id, null, f.Code.Id, null, "NEW", new(2026,11,13), "New expense", 50m, 0m, "ETB", 1m, null, null));
            var saved = f.Db.Expenses.Single(x => x.Id == row.Id);
            Assert.Equal(f.Actor.Id, saved.CreatedBy); Assert.Equal(f.Actor.Id, saved.UpdatedBy);
        }
    }

    [Theory]
    [InlineData("budget", "creator", "FinanceOfficer")]
    [InlineData("budget", "editor", "FinanceOfficer")]
    [InlineData("budget", "unknown", "FinanceOfficer")]
    [InlineData("budget", "creator", "CompanyAdmin")]
    [InlineData("budget", "creator", "SuperAdmin")]
    [InlineData("expense", "creator", "FinanceOfficer")]
    [InlineData("expense", "editor", "FinanceOfficer")]
    [InlineData("expense", "unknown", "FinanceOfficer")]
    [InlineData("expense", "creator", "CompanyAdmin")]
    [InlineData("expense", "creator", "SuperAdmin")]
    public async Task Approval_cannot_certify_own_or_unattributed_work(string kind, string origin, string role)
    {
        using var f = new Fixture(role);
        Guid? creator = origin == "unknown" ? null : origin == "creator" ? f.Actor.Id : Guid.NewGuid();
        Guid? editor = origin == "editor" ? f.Actor.Id : creator;
        if (kind == "budget")
        {
            var row = new Budget { CompanyId = f.Site.CompanyId, ConstructionSiteId = f.Site.Id, BudgetNumber = "B", EffectiveFrom = new DateOnly(2026,11,13), Status = BudgetStatus.PendingApproval, CreatedBy = creator, UpdatedBy = editor };
            f.Db.Budgets.Add(row); await f.Db.SaveChangesAsync();
            await Assert.ThrowsAsync<BudgetManagementException>(() => new BudgetService(f.Db, new PermissionEvaluator(f.Db)).ChangeStatusAsync(f.Actor.Id, row.Id, new(BudgetStatus.Approved)));
            Assert.Equal(BudgetStatus.PendingApproval, row.Status); Assert.Null(row.ApprovedBy);
        }
        else
        {
            var row = new Expense { CompanyId = f.Site.CompanyId, ConstructionSiteId = f.Site.Id, CostCodeId = f.Code.Id, ExpenseNumber = "E", Status = ExpenseStatus.Submitted, CreatedBy = creator, UpdatedBy = editor };
            f.Db.Expenses.Add(row); await f.Db.SaveChangesAsync();
            await Assert.ThrowsAsync<ExpenseManagementException>(() => new ExpenseService(f.Db).ChangeStatusAsync(f.Actor.Id, row.Id, new(ExpenseStatus.Approved)));
            Assert.Equal(ExpenseStatus.Submitted, row.Status); Assert.Null(row.ApprovedBy);
        }
    }

    [Theory]
    [InlineData("budget")] [InlineData("expense")]
    public async Task Independent_authorized_checker_can_approve(string kind)
    {
        using var f = new Fixture(); var maker = Guid.NewGuid();
        if (kind == "budget")
        {
            var row = new Budget { CompanyId = f.Site.CompanyId, ConstructionSiteId = f.Site.Id, BudgetNumber = "B", EffectiveFrom = new DateOnly(2026,11,13), Status = BudgetStatus.PendingApproval, CreatedBy = maker, UpdatedBy = maker };
            f.Db.Budgets.Add(row); await f.Db.SaveChangesAsync();
            var result = await new BudgetService(f.Db, new PermissionEvaluator(f.Db)).ChangeStatusAsync(f.Actor.Id, row.Id, new(BudgetStatus.Approved));
            Assert.Equal(BudgetStatus.Approved, result.Status); Assert.Equal(f.Actor.Id, result.ApprovedBy); Assert.Equal(maker, row.UpdatedBy);
        }
        else
        {
            var row = new Expense { CompanyId = f.Site.CompanyId, ConstructionSiteId = f.Site.Id, CostCodeId = f.Code.Id, ExpenseNumber = "E", Status = ExpenseStatus.Submitted, CreatedBy = maker, UpdatedBy = maker };
            f.Db.Expenses.Add(row); await f.Db.SaveChangesAsync();
            var result = await new ExpenseService(f.Db).ChangeStatusAsync(f.Actor.Id, row.Id, new(ExpenseStatus.Approved));
            Assert.Equal(ExpenseStatus.Approved, result.Status); Assert.Equal(f.Actor.Id, result.ApprovedBy); Assert.Equal(maker, row.UpdatedBy);
        }
    }

    [Theory]
    [InlineData(BudgetStatus.Approved)] [InlineData(BudgetStatus.Active)]
    public async Task Approved_budget_lines_cannot_be_changed_without_new_approval(BudgetStatus status)
    {
        using var f = new Fixture();
        var budget = new Budget { CompanyId = f.Site.CompanyId, ConstructionSiteId = f.Site.Id, BudgetNumber = "B", EffectiveFrom = new DateOnly(2026,11,13), Status = status, CreatedBy = Guid.NewGuid() };
        f.Db.Budgets.Add(budget); await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<BudgetLineManagementException>(() => new BudgetLineService(f.Db).CreateAsync(f.Actor.Id, new(budget.Id, f.Code.Id, "Changed", 100m, 100m)));
        Assert.Empty(f.Db.BudgetLines);
    }

    [Fact]
    public async Task Budget_line_editor_cannot_approve_the_parent_they_just_changed()
    {
        using var f = new Fixture();
        var budget = new Budget { CompanyId = f.Site.CompanyId, ConstructionSiteId = f.Site.Id, BudgetNumber = "B", EffectiveFrom = new DateOnly(2026,11,13), Status = BudgetStatus.PendingApproval, CreatedBy = Guid.NewGuid() };
        f.Db.Budgets.Add(budget); await f.Db.SaveChangesAsync();
        await new BudgetLineService(f.Db).CreateAsync(f.Actor.Id, new(budget.Id, f.Code.Id, "Changed", 100m, 100m));
        Assert.Equal(f.Actor.Id, budget.UpdatedBy);
        await Assert.ThrowsAsync<BudgetManagementException>(() => new BudgetService(f.Db, new PermissionEvaluator(f.Db)).ChangeStatusAsync(f.Actor.Id, budget.Id, new(BudgetStatus.Approved)));
    }

    [Theory]
    [InlineData("budget", "maker", false)] [InlineData("budget", "checker", true)] [InlineData("budget", "unknown", false)]
    [InlineData("expense", "maker", false)] [InlineData("expense", "checker", true)] [InlineData("expense", "unknown", false)]
    [InlineData("budget", "revoked", false)] [InlineData("expense", "revoked", false)]
    [InlineData("budget", "mixed-unassigned", false)] [InlineData("expense", "mixed-unassigned", false)]
    [InlineData("budget", "lowercase", true)] [InlineData("expense", "lowercase", true)]
    public async Task Detail_capability_respects_provenance_live_grant_and_action_scope(string kind, string situation, bool allowed)
    {
        using var f = new Fixture(situation == "lowercase" ? "financeofficer" : "FinanceOfficer");
        var siteId = f.Site.Id;
        if (situation == "revoked")
        {
            var permission = kind == "budget" ? GranularPermission.Finance_Budget_Manage : GranularPermission.Finance_Expense_Approve;
            f.Db.RoleGranularPermissions.Remove(f.Actor.UserRoles.Single().Role.GranularPermissions.Single(x => x.Permission == permission));
        }
        if (situation == "mixed-unassigned")
        {
            var other = new ConstructionSite { CompanyId = f.Site.CompanyId, SiteCode = "OTHER", Name = "Unassigned", IsActive = true };
            f.Db.ConstructionSites.Add(other); siteId = other.Id;
            f.Db.UserRoles.Add(new UserRole { UserId = f.Actor.Id, Role = FinanceOfficerAuthorityTests.Role("Accountant", kind == "budget" ? GranularPermission.Finance_Budget_View : GranularPermission.Finance_Expense_View) });
        }
        Guid? maker = situation == "unknown" ? null : situation == "maker" ? f.Actor.Id : Guid.NewGuid();
        object response;
        if (kind == "budget")
        {
            var row = new Budget { CompanyId = f.Site.CompanyId, ConstructionSiteId = siteId, BudgetNumber = "CAP", EffectiveFrom = new(2026,11,13), Status = BudgetStatus.PendingApproval, CreatedBy = maker, UpdatedBy = maker };
            f.Db.Budgets.Add(row); await f.Db.SaveChangesAsync();
            response = await new BudgetService(f.Db, new PermissionEvaluator(f.Db)).GetByIdAsync(f.Actor.Id, row.Id);
        }
        else
        {
            var row = new Expense { CompanyId = f.Site.CompanyId, ConstructionSiteId = siteId, CostCodeId = f.Code.Id, ExpenseNumber = "CAP", Status = ExpenseStatus.Submitted, CreatedBy = maker, UpdatedBy = maker };
            f.Db.Expenses.Add(row); await f.Db.SaveChangesAsync();
            response = await new ExpenseService(f.Db).GetByIdAsync(f.Actor.Id, row.Id);
        }
        using var json = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(response));
        Assert.Equal(allowed, json.RootElement.GetProperty("CanApprove").GetBoolean());
        if (!allowed) Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("ApprovalBlockReason").GetString()));
    }

    [Theory]
    [InlineData("budget")] [InlineData("expense")]
    public async Task Header_editor_is_the_maker_for_subsequent_approval(string kind)
    {
        using var f = new Fixture(); var maker = Guid.NewGuid();
        if (kind == "budget")
        {
            var row = new Budget { CompanyId = f.Site.CompanyId, ConstructionSiteId = f.Site.Id, BudgetNumber = "EDIT", Name = "Old", EffectiveFrom = new(2026,11,13), Status = BudgetStatus.Draft, CreatedBy = maker, UpdatedBy = maker };
            f.Db.Budgets.Add(row); await f.Db.SaveChangesAsync(); var service = new BudgetService(f.Db, new PermissionEvaluator(f.Db));
            await service.UpdateAsync(f.Actor.Id, row.Id, new("Changed", null, 100m, "ETB", new DateOnly(2026,11,13), null));
            Assert.Equal(f.Actor.Id, (await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(f.Db.Budgets, x => x.Id == row.Id)).UpdatedBy);
            await service.ChangeStatusAsync(f.Actor.Id, row.Id, new(BudgetStatus.PendingApproval));
            await Assert.ThrowsAsync<BudgetManagementException>(() => service.ChangeStatusAsync(f.Actor.Id, row.Id, new(BudgetStatus.Approved)));
        }
        else
        {
            var row = new Expense { CompanyId = f.Site.CompanyId, ConstructionSiteId = f.Site.Id, CostCodeId = f.Code.Id, ExpenseNumber = "EDIT", Description = "Old", Status = ExpenseStatus.Draft, CreatedBy = maker, UpdatedBy = maker };
            f.Db.Expenses.Add(row); await f.Db.SaveChangesAsync(); var service = new ExpenseService(f.Db);
            await service.UpdateAsync(f.Actor.Id, row.Id, new(f.Site.Id, null, f.Code.Id, null, new(2026,11,13), "Changed", 50m, 0m, "ETB", 1m, null, null));
            Assert.Equal(f.Actor.Id, (await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(f.Db.Expenses, x => x.Id == row.Id)).UpdatedBy);
            await service.ChangeStatusAsync(f.Actor.Id, row.Id, new(ExpenseStatus.Submitted));
            await Assert.ThrowsAsync<ExpenseManagementException>(() => service.ChangeStatusAsync(f.Actor.Id, row.Id, new(ExpenseStatus.Approved)));
        }
    }

    [Theory]
    [InlineData(ExpenseStatus.Approved)] [InlineData(ExpenseStatus.Paid)]
    public async Task Approved_expense_content_cannot_be_changed_under_its_checker(ExpenseStatus status)
    {
        using var f = new Fixture();
        var row = new Expense { CompanyId = f.Site.CompanyId, ConstructionSiteId = f.Site.Id, CostCodeId = f.Code.Id, ExpenseNumber = "LOCKED", Description = "Original", Amount = 50m, Status = status, CreatedBy = Guid.NewGuid(), ApprovedBy = Guid.NewGuid() };
        f.Db.Expenses.Add(row); await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<ExpenseManagementException>(() => new ExpenseService(f.Db).UpdateAsync(f.Actor.Id, row.Id, new(f.Site.Id, null, f.Code.Id, null, new(2026,11,13), "Changed", 500m, 0m, "ETB", 1m, null, null)));
        Assert.Equal(50m, row.Amount);
    }

    internal sealed class Fixture : IDisposable
    {
        internal readonly FinanceOfficerAuthorityTests.TestDb Db = FinanceOfficerAuthorityTests.Database();
        internal readonly User Actor;
        internal readonly ConstructionSite Site;
        internal readonly CostCode Code;
        internal Fixture(string role = "FinanceOfficer")
        {
            Actor = FinanceOfficerAuthorityTests.Actor();
            if (role != "FinanceOfficer") Actor.UserRoles = [new() { Role = FinanceOfficerAuthorityTests.Role(role, FinanceOfficerAuthorityTests.Defaults) }];
            Site = new() { CompanyId = Actor.CompanyId!.Value, SiteCode = "SITE", Name = "Assigned", IsActive = true };
            Code = new() { CompanyId = Site.CompanyId, Code = "COST", Name = "Cost", IsActive = true };
            Db.Users.Add(Actor); Db.Companies.Add(new() { Id = Site.CompanyId, Code = "CO", Name = "Company", IsActive = true });
            Db.ConstructionSites.Add(Site); Db.CostCodes.Add(Code);
            Db.UserSiteAccesses.Add(new() { UserId = Actor.Id, CompanyId = Site.CompanyId, SiteId = Site.Id, IsActive = true });
            Db.SaveChanges(true);
        }
        public void Dispose() => Db.Dispose();
    }
}
