using CSM.Application.Common.Exceptions;
using CSM.Domain.Entities.Finance;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Security;
using CSM.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class SiteManagerFinanceScopeTests
{
    [Theory]
    [InlineData("SiteManager", 1)]
    [InlineData("CompanyAdmin", 3)]
    [InlineData("Accountant", 3)]
    public async Task Lists_return_only_the_finance_scope_the_actor_can_read(string role, int count)
    {
        using var db = Database(role);
        var budgets = await new BudgetService(db, new Permissions()).GetAllAsync(Actor);
        var payments = await new PaymentService(db).GetAllAsync(Actor);
        Assert.Equal(count, budgets.Count);
        Assert.Equal(count, payments.Count);
        if (role == "SiteManager")
        {
            Assert.Equal(Allowed, Assert.Single(budgets).ConstructionSiteId);
            Assert.Equal(Allowed, Assert.Single(payments).ConstructionSiteId);
        }
    }

    [Theory]
    [InlineData("company")]
    [InlineData("unassigned")]
    [InlineData("foreign")]
    public async Task Budget_guessed_ids_cannot_bypass_assigned_site_scope(string record)
    {
        using var db = Database("SiteManager");
        var budget = db.Budgets.Single(x => x.BudgetNumber == record);
        await Assert.ThrowsAsync<BudgetManagementException>(() => new BudgetService(db, new Permissions()).GetByIdAsync(Actor, budget.Id));
    }

    [Fact]
    public async Task Explicit_company_finance_endpoints_reject_site_manager()
    {
        using var db = Database("SiteManager");
        await Assert.ThrowsAsync<ExpenseManagementException>(() => new ExpenseService(db).GetAllAsync(Actor));
        await Assert.ThrowsAsync<PaymentManagementException>(() => new PaymentService(db).GetCompanyPaymentsAsync(Actor));
        await Assert.ThrowsAsync<PettyCashManagementException>(() => new PettyCashAccountService(db).GetAllAsync(Actor));
    }

    [Theory]
    [InlineData("active")]
    [InlineData("inactive")]
    [InlineData("deleted")]
    [InlineData("wrong-company")]
    [InlineData("deleted-site")]
    [InlineData("missing")]
    public async Task All_site_finance_read_paths_require_a_live_own_company_grant(string grantState)
    {
        using var db = Database("SiteManager");
        var grant = db.UserSiteAccesses.Single();
        if (grantState == "inactive") grant.IsActive = false;
        if (grantState == "deleted") grant.IsDeleted = true;
        if (grantState == "wrong-company") grant.CompanyId = OtherCompany;
        if (grantState == "deleted-site") db.ConstructionSites.Single(x => x.Id == Allowed).IsDeleted = true;
        if (grantState == "missing") db.UserSiteAccesses.Remove(grant);
        db.SaveChanges(true);
        var budgetId = db.Budgets.Single(x => x.BudgetNumber == "allowed").Id;
        var paymentId = db.Payments.Single(x => x.PaymentNumber == "allowed").Id;
        var budget = new BudgetService(db, new Permissions());
        var reads = new Func<Task>[]
        {
            async () => { await budget.GetBySiteAsync(Actor, Allowed); },
            async () => { await budget.GetByIdAsync(Actor, budgetId); },
            async () => { await new PaymentService(db).GetByIdAsync(Actor, paymentId); },
            async () => { await new ExpenseService(db).GetBySiteAsync(Actor, Allowed); },
            async () => { await new PettyCashAccountService(db).GetSiteAccountsAsync(Actor, Allowed); },
            async () => { await new PettyCashAccountService(db).GetByIdAsync(Actor, CashAccount); },
            async () => { await new PettyCashTransactionService(db).GetAllAsync(Actor, CashAccount); }
        };
        var errors = new[] { typeof(BudgetManagementException), typeof(BudgetManagementException), typeof(PaymentManagementException), typeof(ExpenseManagementException), typeof(PettyCashManagementException), typeof(PettyCashManagementException), typeof(PettyCashManagementException) };
        for (var i = 0; i < reads.Length; i++)
        {
            if (grantState == "active") await reads[i]();
            else Assert.IsType(errors[i], await Record.ExceptionAsync(reads[i]));
        }
        if (grantState != "active")
        {
            Assert.Empty(await budget.GetAllAsync(Actor));
            Assert.Empty(await new PaymentService(db).GetAllAsync(Actor));
        }
    }

    [Theory]
    [InlineData("CompanyAdmin")]
    [InlineData("Accountant")]
    public async Task Corporate_roles_keep_company_records_without_site_assignments(string role)
    {
        using var db = Database(role);
        db.UserSiteAccesses.RemoveRange(db.UserSiteAccesses);
        db.SaveChanges(true);
        var id = db.Budgets.Single(x => x.BudgetNumber == "company").Id;
        Assert.Null((await new BudgetService(db, new Permissions()).GetByIdAsync(Actor, id)).ConstructionSiteId);
        await new ExpenseService(db).GetAllAsync(Actor);
        await new PaymentService(db).GetCompanyPaymentsAsync(Actor);
        await new PettyCashAccountService(db).GetAllAsync(Actor);
    }

    [Theory]
    [InlineData("budget")]
    [InlineData("expenses")]
    [InlineData("payments")]
    public async Task Company_read_controllers_return_forbidden_for_site_manager(string kind)
    {
        using var db = Database("SiteManager");
        var identity = new System.Security.Claims.ClaimsIdentity([new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, Actor.ToString())], "test");
        var controllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = new System.Security.Claims.ClaimsPrincipal(identity) } };
        Microsoft.AspNetCore.Mvc.IActionResult? result;
        if (kind == "budget")
        {
            var controller = new CSM.Api.Controllers.BudgetsController(new BudgetService(db, new Permissions())) { ControllerContext = controllerContext };
            result = (await controller.GetById(db.Budgets.Single(x => x.BudgetNumber == "company").Id, default)).Result;
        }
        else if (kind == "expenses")
        {
            var controller = new CSM.Api.Controllers.ExpensesController(new ExpenseService(db)) { ControllerContext = controllerContext };
            result = (await controller.GetAll(default)).Result;
        }
        else
        {
            var service = new PaymentService(db);
            var controller = new CSM.Api.Controllers.PaymentsController(service, new PaymentReceiptPdfService(service)) { ControllerContext = controllerContext };
            result = (await controller.GetCompany(default)).Result;
        }
        Assert.Equal(403, Assert.IsAssignableFrom<Microsoft.AspNetCore.Mvc.ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task Explicit_corporate_role_preserves_company_scope_for_mixed_role_user()
    {
        using var db = Database("SiteManager");
        db.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = Actor, Role = new Role { Id = Guid.NewGuid(), Name = "CompanyAdmin" } });
        db.SaveChanges(true);
        Assert.Equal(3, (await new BudgetService(db, new Permissions()).GetAllAsync(Actor)).Count);
        var id = db.Budgets.Single(x => x.BudgetNumber == "company").Id;
        Assert.Null((await new BudgetService(db, new Permissions()).GetByIdAsync(Actor, id)).ConstructionSiteId);
        await new ExpenseService(db).GetAllAsync(Actor);
        await new PettyCashAccountService(db).GetAllAsync(Actor);
    }

    [Theory]
    [InlineData("active")]
    [InlineData("inactive")]
    [InlineData("deleted")]
    [InlineData("wrong-company")]
    [InlineData("deleted-site")]
    [InlineData("missing")]
    public async Task New_finance_notifications_require_current_site_access(string grantState)
    {
        using var db = Database("SiteManager");
        var grant = db.UserSiteAccesses.Single();
        if (grantState == "inactive") grant.IsActive = false;
        if (grantState == "deleted") grant.IsDeleted = true;
        if (grantState == "wrong-company") grant.CompanyId = OtherCompany;
        if (grantState == "deleted-site") db.ConstructionSites.Single(x => x.Id == Allowed).IsDeleted = true;
        if (grantState == "missing") db.UserSiteAccesses.Remove(grant);
        db.RoleGranularPermissions.Add(new RoleGranularPermission { Id = Guid.NewGuid(), RoleId = db.Roles.Single().Id, Permission = CSM.Domain.Enums.GranularPermission.Finance_Budget_View });
        db.SaveChanges(true);
        await new NotificationPublisher(db).StageForPermissionHoldersAsync(Company, Allowed,
            CSM.Domain.Enums.GranularPermission.Finance_Budget_View, null, "Budget warning", "Allowed-site figures",
            CSM.Domain.Enums.NotificationSeverity.Warning, "Budget", Guid.NewGuid());
        Assert.Equal(grantState == "active" ? 1 : 0, db.Notifications.Local.Count);
    }

    private static readonly Guid Actor = Guid.NewGuid(), Company = Guid.NewGuid(), OtherCompany = Guid.NewGuid();
    private static readonly Guid Allowed = Guid.NewGuid(), Unassigned = Guid.NewGuid(), Foreign = Guid.NewGuid(), CashAccount = Guid.NewGuid();
    private static ApplicationDbContext Database(string role)
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Users.Add(new User { Id = Actor, CompanyId = Company, IsActive = true, UserRoles = [new UserRole { Id = Guid.NewGuid(), Role = new Role { Id = Guid.NewGuid(), Name = role } }] });
        db.ConstructionSites.AddRange(new ConstructionSite { Id = Allowed, CompanyId = Company, SiteCode = "ALLOWED", Name = "Allowed" },
            new ConstructionSite { Id = Unassigned, CompanyId = Company, SiteCode = "OTHER", Name = "Unassigned" },
            new ConstructionSite { Id = Foreign, CompanyId = OtherCompany, SiteCode = "FOREIGN", Name = "Foreign" });
        db.UserSiteAccesses.Add(new UserSiteAccess { Id = Guid.NewGuid(), UserId = Actor, CompanyId = Company, SiteId = Allowed, IsActive = true });
        foreach (var (number, site, company) in new (string, Guid?, Guid)[] { ("allowed", Allowed, Company), ("unassigned", Unassigned, Company), ("company", null, Company), ("foreign", Foreign, OtherCompany) })
        {
            db.Budgets.Add(new Budget { Id = Guid.NewGuid(), CompanyId = company, ConstructionSiteId = site, BudgetNumber = number, Name = number, CurrencyCode = "ETB", EffectiveFrom = new DateOnly(2026, 11, 3) });
            db.Payments.Add(new Payment { Id = Guid.NewGuid(), CompanyId = company, ConstructionSiteId = site, PaymentNumber = number, CurrencyCode = "ETB" });
        }
        db.PettyCashAccounts.Add(new PettyCashAccount { Id = CashAccount, CompanyId = Company, ConstructionSiteId = Allowed, AccountCode = "TEST", AccountName = "Test" });
        db.SaveChanges(true);
        return db;
    }
    private sealed class Permissions : IPermissionEvaluator
    {
        public Task<bool> HasPermissionAsync(Guid id, string permission, CancellationToken token = default) => Task.FromResult(true);
        public Task<IReadOnlyCollection<string>> GetPermissionsAsync(Guid id, CancellationToken token = default) => Task.FromResult<IReadOnlyCollection<string>>([]);
    }
}
