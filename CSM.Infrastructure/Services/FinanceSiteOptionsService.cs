using CSM.Application.Finance.Options;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

/// <summary>Minimal finance references authorized by the action being prepared, without granting project or procurement access.</summary>
public sealed class FinanceSiteOptionsService(ApplicationDbContext db) : IFinanceSiteOptionsService
{
    public async Task<FinanceActionCapabilities> GetActionCapabilitiesAsync(Guid actorId, string purpose, CancellationToken cancellationToken = default)
    {
        var action = purpose switch
        {
            "payment" => GranularPermission.Finance_Payment_Manage,
            "petty-cash" => GranularPermission.Finance_PettyCash_Manage,
            _ => throw new ArgumentException("Unknown finance action.", nameof(purpose))
        };
        var permissions = new PermissionEvaluator(db);
        if (!await permissions.HasPermissionAsync(actorId, action.ToString(), cancellationToken))
            throw new UnauthorizedAccessException("The finance action is not permitted.");
        var actor = await FinanceOfficerScope.ResolveActorAsync(db, actorId, action, cancellationToken,
            message => new UnauthorizedAccessException(message), supportedIndependentRoles: ["SuperAdmin", "CompanyAdmin", "Accountant"])
            ?? await db.Users.AsNoTracking().Include(x => x.UserRoles).ThenInclude(x => x.Role).ThenInclude(x => x.GranularPermissions)
                .SingleOrDefaultAsync(x => x.Id == actorId && x.IsActive && !x.IsDeleted, cancellationToken)
            ?? throw new UnauthorizedAccessException("Authenticated user was not found.");
        var corporate = actor.CompanyId.HasValue && actor.CompanyId != Guid.Empty && actor.UserRoles.Any(x => !x.IsDeleted && !x.Role.IsDeleted &&
            (x.Role.Name is "SuperAdmin" or "CompanyAdmin" or "Accountant") && x.Role.GranularPermissions.Any(g => g.Permission == action));
        var officer = FinanceOfficerScope.IsOfficer(actor);
        if (!corporate && !officer) throw new UnauthorizedAccessException("The finance action is not permitted.");
        return new(corporate, corporate && purpose == "payment" &&
            await permissions.HasPermissionAsync(actorId, "HRM_Payroll_View", cancellationToken));
    }

    public async Task<FinanceSiteOptionsResponse> GetAsync(Guid actorId, Guid siteId, string purpose, CancellationToken cancellationToken = default)
    {
        var action = purpose switch {
            "budget" => GranularPermission.Finance_Budget_Manage,
            "invoice" => GranularPermission.Finance_Invoice_View,
            "expense" => GranularPermission.Finance_Expense_Submit,
            "payment" => GranularPermission.Finance_Payment_Manage,
            _ => throw new ArgumentException("Unknown finance form.", nameof(purpose))
        };
        if (!await new PermissionEvaluator(db).HasPermissionAsync(actorId, action.ToString(), cancellationToken))
            throw new UnauthorizedAccessException("This finance action is not permitted.");
        var actor = await FinanceOfficerScope.ResolveActorAsync(db, actorId, action, cancellationToken,
            message => new UnauthorizedAccessException(message), supportedIndependentRoles: purpose == "budget" ? null :
                purpose == "expense" ? ["SuperAdmin", "CompanyAdmin", "Accountant", "SiteManager"] : ["SuperAdmin", "CompanyAdmin", "Accountant"]) ?? await db.Users.AsNoTracking()
                .Include(x => x.UserRoles).ThenInclude(x => x.Role)
                .SingleOrDefaultAsync(x => x.Id == actorId && x.IsActive && !x.IsDeleted, cancellationToken)
                ?? throw new UnauthorizedAccessException("Authenticated user was not found.");
        var site = await db.ConstructionSites.AsNoTracking().SingleOrDefaultAsync(x => x.Id == siteId && !x.IsDeleted && x.IsActive, cancellationToken)
            ?? throw new KeyNotFoundException("Active site was not found.");
        var corporate = actor.UserRoles.Any(x => !x.IsDeleted && !x.Role.IsDeleted && x.Role.Name is "SuperAdmin" or "CompanyAdmin" or "Accountant");
        var superAdmin = actor.UserRoles.Any(x => !x.IsDeleted && !x.Role.IsDeleted && x.Role.Name == "SuperAdmin");
        if ((!superAdmin && actor.CompanyId != site.CompanyId) ||
            (!corporate && !await SiteFinanceScope.HasSiteAsync(db, actor, siteId, cancellationToken)))
            throw new UnauthorizedAccessException("The selected site is outside your assigned scope.");
        var projects = await db.Projects.AsNoTracking().Where(x => x.CompanyId == site.CompanyId && x.ConstructionSiteId == siteId && !x.IsDeleted)
            .OrderBy(x => x.ProjectCode).Select(x => new FinanceProjectOption(x.Id, x.ProjectCode, x.Name, x.ConstructionSiteId)).ToArrayAsync(cancellationToken);
        var codes = purpose is "budget" or "expense"
            ? await db.CostCodes.AsNoTracking().Where(x => x.CompanyId == site.CompanyId && !x.IsDeleted && x.IsActive && (x.ConstructionSiteId == null || x.ConstructionSiteId == siteId))
                .OrderBy(x => x.Code).Select(x => new FinanceCostCodeOption(x.Id, x.Code, x.Name, x.ConstructionSiteId, x.IsActive)).ToArrayAsync(cancellationToken)
            : Array.Empty<FinanceCostCodeOption>();
        var vendors = purpose is "invoice" or "expense" or "payment"
            ? await db.Vendors.AsNoTracking().Where(x => x.CompanyId == site.CompanyId && !x.IsDeleted && x.IsActive)
                .OrderBy(x => x.VendorCode).Select(x => new FinanceVendorOption(x.Id, x.VendorCode, x.Name, x.IsActive)).ToArrayAsync(cancellationToken)
            : Array.Empty<FinanceVendorOption>();
        return new(site.Id, site.CompanyId, site.SiteCode, site.Name, projects, codes, vendors);
    }
}
