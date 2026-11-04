using System.Reflection;
using System.Text.Json;
using Bunit;
using CSM.Application.Common.Exceptions;
using CSM.Application.Finance.PettyCashTransactions.Dtos;
using CSM.Domain.Entities.Finance;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Services;
using CSM.Web.Components.Pages.Finance;
using CSM.Web.Models.Finance;
using CSM.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Request = CSM.Application.Finance.PettyCashTransactions.Dtos.CreatePettyCashTransactionRequest;

namespace CSM.Infrastructure.Tests;

public sealed class PettyCashTransactionOptionsTests
{
    private static readonly Guid ActorId = Guid.NewGuid(), CompanyId = Guid.NewGuid(), OtherCompanyId = Guid.NewGuid(), AccountId = Guid.NewGuid(), CodeId = Guid.NewGuid(), OtherCodeId = Guid.NewGuid(), SiteId = Guid.NewGuid();

    [Theory]
    [InlineData("CashOut", true)]
    [InlineData("CashIn", false)]
    public void Transaction_form_loads_scoped_codes_without_catalog_access_and_saves_correct_account(string type, bool code)
    {
        using var context = Context(out var state);
        var page = context.RenderComponent<PettyCashTransactionCreate>(p => p.Add(x => x.AccountId, AccountId));
        page.WaitForAssertion(() => Assert.Contains("LAB - Labour", page.Markup));
        page.Find("select").Change(type);
        page.Find("input").Change("ACC-PC-TXN-001");
        page.Find("input[type=number]").Change("300");
        page.FindAll("input").Last().Change("Petty cash transaction test.");
        if (code) page.FindAll("select").Last().Change(CodeId.ToString());
        page.Find("textarea").Change("Transaction remarks.");
        page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.NotNull(state.Saved));
        Assert.Equal(AccountId, state.Saved!.PettyCashAccountId);
        Assert.Equal(type, state.Saved.TransactionType);
        Assert.Equal(300m, state.Saved.Amount);
        Assert.Equal(code ? CodeId : (Guid?)null, state.Saved.CostCodeId);
        Assert.Equal("ACC-PC-TXN-001", state.Saved.ReferenceNumber);
        Assert.Equal("Petty cash transaction test.", state.Saved.Description);
        Assert.Equal("Transaction remarks.", state.Saved.Remarks);
        page.WaitForAssertion(() => Assert.Equal($"http://localhost/finance/petty-cash/account/{AccountId}/transactions", context.Services.GetRequiredService<NavigationManager>().Uri));
    }

    [Fact]
    public void Lookup_failure_ends_loading_and_does_not_expose_a_savable_form()
    {
        using var context = Context(out var state);
        state.FailOptions = true;
        var page = context.RenderComponent<PettyCashTransactionCreate>(p => p.Add(x => x.AccountId, AccountId));
        page.WaitForAssertion(() => Assert.Contains("Code lookup denied", page.Markup));
        Assert.DoesNotContain("Loading account", page.Markup);
        Assert.Empty(page.FindAll("form"));
        Assert.Null(state.Saved);
    }

    [Theory]
    [InlineData("Reference")]
    [InlineData("Description")]
    [InlineData("Remarks")]
    public void Current_text_is_submitted_even_without_blurring_the_last_edited_field(string field)
    {
        using var context = Context(out var state);
        state.AllowCatalog = true;
        var page = context.RenderComponent<PettyCashTransactionCreate>(p => p.Add(x => x.AccountId, AccountId));
        page.WaitForAssertion(() => Assert.Single(page.FindAll("form")));
        page.Find("input").Change("old reference");
        page.Find("input[type=number]").Change("300");
        page.FindAll("input").Last().Change("old description");
        page.Find("textarea").Change("old remarks");
        var target = field == "Remarks" ? page.Find("textarea") : field == "Description" ? page.FindAll("input").Last() : page.Find("input");
        try { target.Input("Updated transaction text."); }
        catch (Exception ex) when (ex.GetType().Name == "MissingEventHandlerException") { }
        page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.NotNull(state.Saved));
        Assert.Equal("Updated transaction text.", field == "Reference" ? state.Saved!.ReferenceNumber : field == "Description" ? state.Saved!.Description : state.Saved!.Remarks);
    }

    [Fact]
    public async Task Options_HTTP_requires_manage_in_addition_to_view()
    {
        Assert.NotNull(typeof(CSM.Api.Controllers.PettyCashTransactionsController).GetMethod("GetCostCodeOptions"));
        await new BackendActionPermissionTests().HTTP_action_requires_its_own_permission_even_when_all_role_checks_pass(
            "PettyCashTransactionsController", "GetCostCodeOptions", GranularPermission.Finance_PettyCash_Manage, GranularPermission.Finance_PettyCash_View);
    }

    [Theory]
    [InlineData("Accountant")]
    [InlineData("CompanyAdmin")]
    [InlineData("SuperAdmin")]
    public async Task Corporate_lookup_returns_only_active_account_company_codes_and_minimal_fields(string role)
    {
        using var db = Database(role);
        var rows = await Options(db);
        var row = Assert.Single(rows.EnumerateArray());
        Assert.Equal(CodeId, row.GetProperty("Id").GetGuid());
        Assert.Equal("LAB", row.GetProperty("Code").GetString());
        Assert.Equal(new[] { "Code", "Id", "Name" }, row.EnumerateObject().Select(p => p.Name).Order().ToArray());
    }

    [Theory]
    [InlineData("foreign-account")]
    [InlineData("revoked-grant")]
    [InlineData("inactive-user")]
    [InlineData("deleted-user")]
    [InlineData("deleted-account")]
    [InlineData("company-site-manager")]
    [InlineData("revoked-site")]
    public async Task Options_reject_unauthorized_or_deleted_account_context(string condition)
    {
        using var db = Database(condition is "company-site-manager" or "revoked-site" ? "SiteManager" : "Accountant");
        if (condition == "foreign-account") db.PettyCashAccounts.Single().CompanyId = OtherCompanyId;
        if (condition == "revoked-grant") db.RoleGranularPermissions.RemoveRange(db.RoleGranularPermissions);
        if (condition == "inactive-user") db.Users.Single().IsActive = false;
        if (condition == "deleted-user") db.Users.Single().IsDeleted = true;
        if (condition == "deleted-account") db.PettyCashAccounts.Single().IsDeleted = true;
        if (condition == "revoked-site") { db.PettyCashAccounts.Single().ConstructionSiteId = SiteId; db.UserSiteAccesses.Single().IsActive = false; }
        db.SaveChanges(true);
        await Assert.ThrowsAsync<PettyCashManagementException>(() => Options(db));
    }

    [Fact]
    public async Task Explicitly_granted_site_manager_retains_assigned_site_lookup()
    {
        using var db = Database("SiteManager");
        db.PettyCashAccounts.Single().ConstructionSiteId = SiteId;
        db.SaveChanges(true);
        Assert.Equal(CodeId, Assert.Single((await Options(db)).EnumerateArray()).GetProperty("Id").GetGuid());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Transaction_rejects_foreign_or_deleted_cost_code_before_changing_balance(bool deleted)
    {
        using var db = Database("Accountant");
        var id = deleted ? CodeId : OtherCodeId;
        if (deleted) { db.CostCodes.Single(x => x.Id == id).IsDeleted = true; db.SaveChanges(true); }
        await Assert.ThrowsAsync<PettyCashManagementException>(() => new PettyCashTransactionService(db).CreateAsync(ActorId, Request(id)));
        Assert.Equal(2000m, db.PettyCashAccounts.Single().CurrentBalance);
        Assert.Empty(db.PettyCashTransactions);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Authorized_transaction_keeps_optional_cost_code_and_balance_calculation(bool code)
    {
        using var db = Database("Accountant");
        var tx = await new PettyCashTransactionService(db).CreateAsync(ActorId, Request(code ? CodeId : null));
        Assert.Equal(1700m, db.PettyCashAccounts.Single().CurrentBalance);
        Assert.Equal(2000m, tx.BalanceBefore);
        Assert.Equal(1700m, tx.BalanceAfter);
        Assert.Equal("Remarks", tx.Remarks);
    }

    private static Request Request(Guid? code) => new(AccountId, "CashOut", "ACC-PC-TXN-001", 300m, "Transaction", "Remarks", code);

    private static async Task<JsonElement> Options(ApplicationDbContext db)
    {
        var method = typeof(PettyCashTransactionService).GetMethod("GetCostCodeOptionsAsync");
        Assert.NotNull(method);
        var task = (Task)method.Invoke(new PettyCashTransactionService(db), new object[] { ActorId, AccountId, CancellationToken.None })!;
        await task;
        return JsonSerializer.SerializeToElement(task.GetType().GetProperty("Result")!.GetValue(task));
    }

    private static TestDb Database(string role)
    {
        var db = new TestDb();
        db.Companies.AddRange(new Company { Id = CompanyId, Code = "OWN", Name = "Own" }, new Company { Id = OtherCompanyId, Code = "OTHER", Name = "Other" });
        db.Users.Add(new User { Id = ActorId, CompanyId = CompanyId, IsActive = true, UserRoles = [new() { Role = new Role { Name = role, GranularPermissions = [new() { Permission = GranularPermission.Finance_PettyCash_View }, new() { Permission = GranularPermission.Finance_PettyCash_Manage }] } }] });
        db.ConstructionSites.Add(new ConstructionSite { Id = SiteId, CompanyId = CompanyId, SiteCode = "SITE", Name = "Own site" });
        db.UserSiteAccesses.Add(new UserSiteAccess { UserId = ActorId, SiteId = SiteId, CompanyId = CompanyId, IsActive = true });
        db.PettyCashAccounts.Add(new PettyCashAccount { Id = AccountId, CompanyId = CompanyId, AccountCode = "ACC-PC-TEST-001", AccountName = "Accountant Test Petty Cash Updated", OpeningBalance = 2000, CurrentBalance = 2000 });
        db.CostCodes.AddRange(new CostCode { Id = CodeId, CompanyId = CompanyId, Code = "LAB", Name = "Labour", IsActive = true }, new CostCode { Id = OtherCodeId, CompanyId = OtherCompanyId, Code = "OTHER", Name = "Private", IsActive = true }, new CostCode { CompanyId = CompanyId, Code = "OFF", Name = "Inactive", IsActive = false });
        db.SaveChanges(true);
        return db;
    }

    public sealed class TestDb : ApplicationDbContext
    {
        public TestDb() : base(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options) { }
        public override Task<int> SaveChangesAsync(CancellationToken token = default) => base.SaveChangesAsync(true, token);
    }

    private static TestContext Context(out FinanceProxy state)
    {
        var context = new TestContext(); context.Services.AddLogging();
        var finance = DispatchProxy.Create<IFinanceService, FinanceProxy>(); state = (FinanceProxy)(object)finance;
        context.Services.AddSingleton(finance); return context;
    }

    public class FinanceProxy : DispatchProxy
    {
        public bool AllowCatalog, FailOptions;
        public CSM.Web.Models.Finance.CreatePettyCashTransactionRequest? Saved;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            switch (method!.Name)
            {
                case "GetPettyCashAccountAsync": return Task.FromResult(new PettyCashAccountResponse(AccountId, CompanyId, null, "Company", "ACC-PC-TEST-001", "Accountant Test Petty Cash Updated", 2000m, 2000m, true));
                case "GetCostCodesAsync": return AllowCatalog ? Task.FromResult<IReadOnlyList<CostCodeResponse>>([]) : Task.FromException<IReadOnlyList<CostCodeResponse>>(new HttpRequestException("403 Forbidden"));
                case "GetPettyCashCostCodeOptionsAsync":
                    Assert.Equal(AccountId, args![0]);
                    var type = method.ReturnType.GenericTypeArguments.Single();
                    if (FailOptions) return typeof(Task).GetMethods().Single(m => m.Name == nameof(Task.FromException) && m.IsGenericMethod).MakeGenericMethod(type).Invoke(null, new object[] { new HttpRequestException("Code lookup denied") });
                    var options = JsonSerializer.Deserialize($"[{{\"Id\":\"{CodeId}\",\"Code\":\"LAB\",\"Name\":\"Labour\"}}]", type);
                    return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(type).Invoke(null, new[] { options });
                case "CreatePettyCashTransactionAsync": Saved = (CSM.Web.Models.Finance.CreatePettyCashTransactionRequest)args![0]!; return Task.FromResult(Guid.NewGuid());
                default: throw new NotSupportedException(method.Name);
            }
        }
    }
}
