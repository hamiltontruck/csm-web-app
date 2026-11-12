using CSM.Application.Finance.Invoices.Dtos;
using CSM.Domain.Entities.Finance;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Enums;
using CSM.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace CSM.Infrastructure.Tests;
public sealed class FinanceOfficerReviewRegressionTests
{
    [Theory]
    [InlineData("workspace")] [InlineData("executive")]
    public async Task Independent_site_role_never_borrows_an_ungranted_corporate_scope(string kind)
    {
        using var db = FinanceOfficerAuthorityTests.Database(); var actor = FinanceOfficerAuthorityTests.Actor(); var company = actor.CompanyId!.Value;
        actor.UserRoles.Add(new() { Role = FinanceOfficerAuthorityTests.Role("SiteManager", GranularPermission.Finance_Budget_View, GranularPermission.Finance_Expense_View, GranularPermission.Reports_FinancialSummary_View) });
        actor.UserRoles.Add(new() { Role = FinanceOfficerAuthorityTests.Role("Accountant") }); db.Users.Add(actor);
        db.Companies.Add(new Company { Id = company, Code = "OWN", Name = "Own", IsActive = true });
        var own = Site(company, "A"); var other = Site(company, "B"); db.ConstructionSites.AddRange(own, other);
        db.UserSiteAccesses.Add(new() { UserId = actor.Id, CompanyId = company, SiteId = own.Id, IsActive = true });
        foreach(var site in new[] {own,other}) { var amount = site == own ? 10m : 100m;
            db.Budgets.Add(new Budget { CompanyId = company, ConstructionSiteId = site.Id, BudgetNumber = site.SiteCode, TotalAmount = amount, CurrencyCode = "ETB" });
            db.Expenses.Add(new Expense { CompanyId = company, ConstructionSiteId = site.Id, ExpenseNumber = site.SiteCode, Amount = amount, CurrencyCode = "ETB" }); }
        await db.SaveChangesAsync();
        if(kind == "workspace") {var result = await new WorkspaceDashboardService(db).GetAsync(actor.Id); Assert.Equal(10m,result.TotalBudget); Assert.Equal(10m,result.TotalExpenses);}
        else {var result = await new ExecutiveDashboardService(db).GetForActorAsync(actor.Id,company); Assert.Equal(10m,result.TotalBudget); Assert.Equal(10m,result.TotalExpenses); Assert.Equal(0,result.TotalEmployees);}
    }

    [Theory]
    [InlineData("detail")] [InlineData("list")] [InlineData("status-response")]
    public async Task Invoice_child_payments_and_paid_totals_remain_in_the_parent_site(string route)
    {
        using var db = FinanceOfficerAuthorityTests.Database(); var actor = FinanceOfficerAuthorityTests.Actor(); db.Users.Add(actor); var company = actor.CompanyId!.Value;
        var own = Site(company,"A"); var other = Site(company,"B"); db.ConstructionSites.AddRange(own,other);
        db.UserSiteAccesses.Add(new() { UserId = actor.Id, CompanyId = company, SiteId = own.Id, IsActive = true });
        var invoice = new Invoice { CompanyId=company,ConstructionSiteId=own.Id,InvoiceNumber="OWN",Type=InvoiceType.ClientInvoice,TotalAmount=100m,Subtotal=100m,InvoiceDate=new(2026,11,11),Status=InvoiceStatus.Draft }; db.Invoices.Add(invoice);
        var permitted = new Payment { CompanyId=company,ConstructionSiteId=own.Id,InvoiceId=invoice.Id,PaymentNumber="OWN",Amount=5m,Status=PaymentStatus.Completed }; db.Payments.Add(permitted);
        var secret = new Payment { CompanyId=company,ConstructionSiteId=other.Id,InvoiceId=invoice.Id,PaymentNumber="OTHER-SECRET",Notes="OTHER-NOTES",Amount=75m,Status=PaymentStatus.Completed }; db.Payments.Add(secret);
        await db.SaveChangesAsync(); var service = new InvoiceService(db);
        var response = route == "detail" ? await service.GetByIdAsync(actor.Id,invoice.Id) : route == "list" ? Assert.Single(await service.GetAllAsync(actor.Id,own.Id)) : await service.ChangeStatusAsync(actor.Id,invoice.Id,new ChangeInvoiceStatusRequest(InvoiceStatus.Issued));
        Assert.Equal(5m,response.PaidAmount); Assert.Equal(95m,response.OutstandingAmount);
        Assert.DoesNotContain(response.Payments,x=>x.Id==secret.Id || x.PaymentNumber=="OTHER-SECRET");
        Assert.Equal(invoice.Id,secret.InvoiceId); Assert.Equal(EntityState.Unchanged,db.Entry(secret).State);
    }

    [Fact]
    public async Task Inactive_site_cannot_supply_the_petty_cash_site_list()
    {
        using var db=FinanceOfficerAuthorityTests.Database();var actor=FinanceOfficerAuthorityTests.Actor();db.Users.Add(actor);
        var site=Site(actor.CompanyId!.Value,"INACTIVE");site.IsActive=false;db.ConstructionSites.Add(site);
        db.UserSiteAccesses.Add(new(){UserId=actor.Id,CompanyId=site.CompanyId,SiteId=site.Id,IsActive=true});db.PettyCashAccounts.Add(new(){CompanyId=site.CompanyId,ConstructionSiteId=site.Id,AccountCode="SECRET",AccountName="Secret",CurrentBalance=777m});await db.SaveChangesAsync();
        Assert.NotNull(await Record.ExceptionAsync(()=>new PettyCashAccountService(db).GetSiteAccountsAsync(actor.Id,site.Id)));
    }

    [Theory]
    [InlineData("invoice")] [InlineData("invoice-lines")] [InlineData("budget-lines")]
    public async Task Unsupported_independent_reader_does_not_remove_valid_officer_authority(string kind)
    {
        using var db=FinanceOfficerAuthorityTests.Database();var actor=FinanceOfficerAuthorityTests.Actor();
        actor.UserRoles.Add(new(){Role=FinanceOfficerAuthorityTests.Role("SiteManager",GranularPermission.Finance_Invoice_View,GranularPermission.Finance_Budget_View)});db.Users.Add(actor);
        var site=Site(actor.CompanyId!.Value,"A");db.ConstructionSites.Add(site);db.UserSiteAccesses.Add(new(){UserId=actor.Id,CompanyId=site.CompanyId,SiteId=site.Id,IsActive=true});
        var invoice=new Invoice{CompanyId=site.CompanyId,ConstructionSiteId=site.Id,InvoiceNumber="OWN"};db.Invoices.Add(invoice);var budget=new Budget{CompanyId=site.CompanyId,ConstructionSiteId=site.Id,BudgetNumber="OWN"};db.Budgets.Add(budget);await db.SaveChangesAsync();
        if(kind=="invoice")Assert.Equal(invoice.Id,(await new InvoiceService(db).GetByIdAsync(actor.Id,invoice.Id)).Id);
        else if(kind=="invoice-lines")Assert.Empty(await new InvoiceLineService(db).GetByInvoiceAsync(actor.Id,invoice.Id));
        else Assert.Empty(await new BudgetLineService(db).GetByBudgetAsync(actor.Id,budget.Id));
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task Removed_procurement_membership_cannot_cancel_valid_officer_site_access(bool deleted)
    {
        using var db=FinanceOfficerAuthorityTests.Database();var actor=FinanceOfficerAuthorityTests.Actor();actor.UserRoles.Add(new(){Role=FinanceOfficerAuthorityTests.Role("ProcurementOfficer",deleted?[GranularPermission.Sites_View]:[]),IsDeleted=deleted});db.Users.Add(actor);
        var site=Site(actor.CompanyId!.Value,"A");db.ConstructionSites.Add(site);db.UserSiteAccesses.Add(new(){UserId=actor.Id,CompanyId=site.CompanyId,SiteId=site.Id,IsActive=true});await db.SaveChangesAsync();
        var service=new SiteService(db);Assert.Equal(site.Id,Assert.Single(await service.GetAllAsync(actor.Id)).Id);Assert.Equal(site.Id,(await service.GetByIdAsync(actor.Id,site.Id)).Id);
    }
    [Theory]
    [InlineData("payment",false,false,false)] [InlineData("payment",true,true,true)]
    [InlineData("petty-cash",false,false,false)] [InlineData("petty-cash",true,true,false)]
    public async Task Corporate_form_capabilities_follow_the_actual_manage_grant(string purpose,bool writer,bool companyScope,bool payroll)
    {
        using var db=FinanceOfficerAuthorityTests.Database();var actor=FinanceOfficerAuthorityTests.Actor();
        var grants=new List<GranularPermission>{GranularPermission.Finance_Budget_View,GranularPermission.HRM_Payroll_View};
        if(writer)grants.Add(purpose=="payment"?GranularPermission.Finance_Payment_Manage:GranularPermission.Finance_PettyCash_Manage);
        actor.UserRoles.Add(new(){Role=FinanceOfficerAuthorityTests.Role("Accountant",grants.ToArray())});db.Users.Add(actor);await db.SaveChangesAsync();
        var method=typeof(FinanceSiteOptionsService).GetMethod("GetActionCapabilitiesAsync");Assert.NotNull(method);
        var task=(Task)method.Invoke(new FinanceSiteOptionsService(db),[actor.Id,purpose,CancellationToken.None])!;await task;
        var result=System.Text.Json.JsonSerializer.SerializeToElement(task.GetType().GetProperty("Result")!.GetValue(task));
        Assert.Equal(companyScope,result.GetProperty("IncludesCompany").GetBoolean());Assert.Equal(payroll,result.GetProperty("CanUsePayroll").GetBoolean());
    }
    private static ConstructionSite Site(Guid company,string code)=>new(){CompanyId=company,SiteCode=code,Name=code,IsActive=true};
}
