using CSM.Domain.Entities.Finance;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Enums;
using CSM.Infrastructure.Security;
using CSM.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace CSM.Infrastructure.Tests;

public sealed class FinanceApprovalPostgresTests
{
    [Fact]
    public async Task Retry_enabled_approval_and_its_edit_have_a_valid_serial_order()
    {
        await using var fixture = await FinanceSettlementPostgresFixture.CreateAsync();
        var actor = FinanceOfficerAuthorityTests.Actor();
        var company = new Company { Id = actor.CompanyId!.Value, Code = "CO", Name = "Company", IsActive = true };
        var site = new ConstructionSite { CompanyId = company.Id, SiteCode = "SITE", Name = "Assigned", IsActive = true };
        var code = new CostCode { CompanyId = company.Id, Code = "COST", Name = "Cost", IsActive = true };
        var maker = Guid.NewGuid();
        var budget = new Budget { CompanyId = company.Id, ConstructionSiteId = site.Id, BudgetNumber = "B", Name = "Budget", EffectiveFrom = new(2026,11,13), Status = BudgetStatus.PendingApproval, CreatedBy = maker, UpdatedBy = maker };
        await using (var seed = fixture.Context())
        {
            seed.Companies.Add(company); seed.Users.Add(actor); seed.ConstructionSites.Add(site); seed.CostCodes.Add(code); seed.Budgets.Add(budget);
            seed.UserSiteAccesses.Add(new() { UserId = actor.Id, CompanyId = company.Id, SiteId = site.Id, IsActive = true });
            await seed.SaveChangesAsync();
        }
        await using var editDb = fixture.Context(); await using var approveDb = fixture.Context();
        var edit = Record.ExceptionAsync(() => new BudgetLineService(editDb).CreateAsync(actor.Id, new(budget.Id, code.Id, "Changed", 100m, 100m)));
        var approval = Record.ExceptionAsync(() => new BudgetService(approveDb, new PermissionEvaluator(approveDb)).ChangeStatusAsync(actor.Id, budget.Id, new(BudgetStatus.Approved)));
        var errors = await Task.WhenAll(edit, approval);
        Assert.Single(errors.Where(x => x is null));
        await using var verify = fixture.Context();
        var saved = await verify.Budgets.SingleAsync(x => x.Id == budget.Id);
        if (saved.Status == BudgetStatus.Approved)
        {
            Assert.Equal(actor.Id, saved.ApprovedBy); Assert.Empty(await verify.BudgetLines.ToListAsync());
        }
        else
        {
            Assert.Equal(BudgetStatus.PendingApproval, saved.Status); Assert.Equal(actor.Id, saved.UpdatedBy); Assert.Null(saved.ApprovedBy); Assert.Single(await verify.BudgetLines.ToListAsync());
        }
    }
}
