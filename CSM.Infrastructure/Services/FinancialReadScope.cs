using CSM.Domain.Common;
using CSM.Domain.Entities.Finance;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Entities.Projects;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

/// <summary>Corporate finance roles read their company; other report readers need live site grants.</summary>
internal sealed class FinancialReadScope(ApplicationDbContext db, User actor, Guid companyId)
{
    public bool IncludesCompany { get; } = actor.UserRoles.Any(x => !x.IsDeleted && !x.Role.IsDeleted &&
        new[] { "SuperAdmin", "CompanyAdmin", "Accountant" }.Contains(x.Role.Name, StringComparer.OrdinalIgnoreCase));
    public IQueryable<ConstructionSite> Sites
    {
        get
        {
            var companyScope = IncludesCompany;
            return db.ConstructionSites.AsNoTracking().Where(x => x.CompanyId == companyId && !x.IsDeleted &&
                (companyScope || db.UserSiteAccesses.Any(a => a.UserId == actor.Id && a.CompanyId == companyId && a.SiteId == x.Id && a.IsActive && !a.IsDeleted)));
        }
    }
    public IQueryable<Project> Projects
    {
        get
        {
            var siteIds = Sites.Select(x => x.Id);
            return db.Projects.AsNoTracking().Where(x => x.CompanyId == companyId && !x.IsDeleted && siteIds.Contains(x.ConstructionSiteId));
        }
    }
    public IQueryable<T> Finance<T>(IQueryable<T> rows) where T : TenantEntity
    {
        var companyScope = IncludesCompany;
        var siteIds = Sites.Select(x => x.Id);
        return rows.Where(x => x.CompanyId == companyId && !x.IsDeleted &&
            (companyScope || (EF.Property<Guid?>(x, "ConstructionSiteId").HasValue && siteIds.Contains(EF.Property<Guid?>(x, "ConstructionSiteId")!.Value))));
    }
    public IQueryable<PettyCashTransaction> Transactions
    {
        get
        {
            var accountIds = Finance(db.PettyCashAccounts).Select(x => x.Id);
            return db.PettyCashTransactions.AsNoTracking().Where(x => x.CompanyId == companyId && !x.IsDeleted && accountIds.Contains(x.PettyCashAccountId));
        }
    }
}
