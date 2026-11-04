using System.Reflection;
using System.Text.Json;
using Bunit;
using CSM.Domain.Constants;
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

namespace CSM.Infrastructure.Tests;

public sealed class BudgetLineCostCodeOptionsTests
{
    private static readonly Guid Company = Guid.NewGuid(), OtherCompany = Guid.NewGuid();
    private static readonly Guid Actor = Guid.NewGuid(), BudgetId = Guid.NewGuid(), CodeId = Guid.NewGuid();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Budget_editor_uses_budget_options_without_cost_code_catalog_permission(bool edit)
    {
        using var context = new TestContext();
        context.Services.AddLogging();
        var service = DispatchProxy.Create<IFinanceService, FinanceProxy>();
        context.Services.AddSingleton(service);
        IRenderedFragment page = edit
            ? context.RenderComponent<BudgetLineEdit>(p => p.Add(x => x.BudgetId, BudgetId).Add(x => x.BudgetLineId, CodeId))
            : context.RenderComponent<BudgetLineCreate>(p => p.Add(x => x.BudgetId, BudgetId));
        page.WaitForAssertion(() => Assert.Contains("LAB - Labour", page.Markup));
        Assert.DoesNotContain("403", page.Markup);
        page.Find("select").Change(CodeId.ToString());
        page.Find("input").Change("Accountant budget line test.");
        page.FindAll("input").ElementAt(1).Change("5000");
        page.FindAll("input").ElementAt(2).Change("5000");
        page.Find("form").Submit();
        var saved = ((FinanceProxy)(object)service).Saved;
        Assert.NotNull(saved);
        var json = JsonSerializer.SerializeToElement(saved);
        Assert.Equal(CodeId, json.GetProperty("CostCodeId").GetGuid());
        Assert.Equal("Accountant budget line test.", json.GetProperty("Description").GetString());
        Assert.Equal(5000m, json.GetProperty("BudgetedAmount").GetDecimal());
        Assert.Equal(5000m, json.GetProperty("RevisedAmount").GetDecimal());
        if (!edit) Assert.Equal(BudgetId, json.GetProperty("BudgetId").GetGuid());
        Assert.Equal($"http://localhost/finance/company/budgets/{BudgetId}", context.Services.GetRequiredService<NavigationManager>().Uri);
    }

    [Fact]
    public async Task Lookup_requires_live_budget_manage_grant()
    {
        var action = typeof(CSM.Api.Controllers.BudgetLinesController).GetMethod("GetCostCodeOptions");
        Assert.NotNull(action);
        await new BackendActionPermissionTests().HTTP_action_requires_its_own_permission_even_when_all_role_checks_pass(
            "BudgetLinesController", "GetCostCodeOptions", GranularPermission.Finance_Budget_Manage, GranularPermission.Finance_Budget_View);
    }

    [Fact]
    public async Task Accountant_options_are_active_own_company_only_and_have_no_catalog_metadata()
    {
        using var db = Database(AppRoles.Accountant);
        var options = await Options(db, BudgetId);
        var option = Assert.Single(options.EnumerateArray());
        Assert.Equal(CodeId, option.GetProperty("Id").GetGuid());
        Assert.Equal("LAB", option.GetProperty("Code").GetString());
        Assert.Equal("Labour", option.GetProperty("Name").GetString());
        Assert.Equal(new[] { "Code", "Id", "Name" }, option.EnumerateObject().Select(p => p.Name).Order().ToArray());
    }

    [Theory]
    [InlineData("Accountant", true)]
    [InlineData("SiteManager", false)]
    public async Task Lookup_rejects_other_company_budget_or_ineligible_role(string role, bool other)
    {
        using var db = Database(role);
        var budget = db.Budgets.Single();
        if (other) { budget.CompanyId = OtherCompany; db.SaveChanges(true); }
        await Assert.ThrowsAsync<CSM.Application.Common.Exceptions.BudgetLineManagementException>(() => Options(db, BudgetId));
    }

    [Fact]
    public async Task Lookup_rejects_deleted_budget()
    {
        using var db = Database(AppRoles.Accountant);
        db.Budgets.Single().IsDeleted = true;
        db.SaveChanges(true);
        await Assert.ThrowsAsync<CSM.Application.Common.Exceptions.BudgetLineManagementException>(() => Options(db, BudgetId));
    }

    private static async Task<JsonElement> Options(ApplicationDbContext db, Guid budgetId)
    {
        var method = typeof(BudgetLineService).GetMethod("GetCostCodeOptionsAsync");
        Assert.NotNull(method);
        var task = (Task)method.Invoke(new BudgetLineService(db), new object[] { Actor, budgetId, CancellationToken.None })!;
        await task;
        return JsonSerializer.SerializeToElement(task.GetType().GetProperty("Result")!.GetValue(task));
    }

    private static ApplicationDbContext Database(string role)
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Companies.AddRange(new Company { Id = Company, Name = "Own", Code = "OWN" }, new Company { Id = OtherCompany, Name = "Other", Code = "OTHER" });
        db.Users.Add(new User { Id = Actor, CompanyId = Company, IsActive = true, UserRoles = [new UserRole { Role = new Role { Name = role } }] });
        db.Budgets.Add(new Budget { Id = BudgetId, CompanyId = Company, Name = "Accountant Test Budget", BudgetNumber = "ACC-BUD-TEST-001", TotalAmount = 5000 });
        db.CostCodes.AddRange(new CostCode { Id = CodeId, CompanyId = Company, Code = "LAB", Name = "Labour", IsActive = true },
            new CostCode { CompanyId = OtherCompany, Code = "OTHER", Name = "Secret", IsActive = true },
            new CostCode { CompanyId = Company, Code = "OFF", Name = "Inactive", IsActive = false },
            new CostCode { CompanyId = Company, Code = "DEL", Name = "Deleted", IsActive = true, IsDeleted = true });
        db.SaveChanges(true);
        return db;
    }

    public class FinanceProxy : DispatchProxy
    {
        public object? Saved;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            switch (method!.Name)
            {
                case "GetActiveCostCodesAsync": return Task.FromException<IReadOnlyList<CostCodeResponse>>(new HttpRequestException("403 Forbidden"));
                case "GetBudgetCostCodeOptionsAsync":
                    Assert.Equal(BudgetId, args![0]);
                    var type = method.ReturnType.GenericTypeArguments.Single();
                    var options = JsonSerializer.Deserialize($"[{{\"Id\":\"{CodeId}\",\"Code\":\"LAB\",\"Name\":\"Labour\"}}]", type);
                    return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(type).Invoke(null, new[] { options });
                case "GetBudgetLineAsync": return Task.FromResult(new BudgetLineResponse { Id = CodeId, BudgetId = BudgetId, CompanyId = Company, CostCodeId = CodeId, BudgetedAmount = 1000, RevisedAmount = 1000 });
                case "CreateBudgetLineAsync": Saved = args![0]; return Task.CompletedTask;
                case "UpdateBudgetLineAsync": Saved = args![1]; return Task.CompletedTask;
                default: throw new NotSupportedException(method.Name);
            }
        }
    }
}
