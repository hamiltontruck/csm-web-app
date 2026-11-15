using CSM.Application.Common.Dtos;
using CSM.Domain.Constants;
using CSM.Domain.Enums;
using CSM.Domain.Security;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace CSM.Infrastructure.Security;

/// <summary>Resolves ownership after the caller authorizes its existing action.
/// Never mutates the account or treats a missing company as global authority.</summary>
public static class CompanyContextResolver
{
    public static async Task<bool> HasGlobalAuthorityAsync(ApplicationDbContext db, Guid actorId,
        GranularPermission[] required, CancellationToken token = default)
    {
        if (required.Length == 0 || required.Any(x => !RolePermissionRules.Allows(AppRoles.SuperAdmin, x.ToString()))) return false;
        var roles = await db.UserRoles.AsNoTracking()
            .Where(x => x.UserId == actorId && !x.IsDeleted && x.User.IsActive && !x.User.IsDeleted &&
                !x.Role.IsDeleted && x.Role.Name == AppRoles.SuperAdmin)
            .Select(x => x.RoleId).Distinct().ToListAsync(token);
        if (roles.Count == 0) return false;
        var grants = await db.RoleGranularPermissions.AsNoTracking().Where(x => roles.Contains(x.RoleId))
            .Select(x => new { x.RoleId, x.Permission }).ToListAsync(token);
        return roles.Any(role => required.All(permission => grants.Any(x => x.RoleId == role && x.Permission == permission)));
    }

    public static async Task<Guid> ResolveAsync(ApplicationDbContext db, Guid? actorCompanyId,
        bool allowGlobal, Guid? selectedCompanyId, Func<string, Exception> error, CancellationToken token = default)
    {
        if (!allowGlobal)
        {
            var own = RequireOwnCompany(actorCompanyId, error);
            if (selectedCompanyId.HasValue && selectedCompanyId != own)
                throw error("You cannot select a company outside your authorized company.");
            return own;
        }
        var selected = selectedCompanyId ?? actorCompanyId;
        if (!selected.HasValue || selected == Guid.Empty) throw error("Select a company before continuing.");
        if (!await db.Companies.AsNoTracking().AnyAsync(x => x.Id == selected && x.IsActive && !x.IsDeleted, token))
            throw error("The selected company is invalid or inactive.");
        return selected.Value;
    }

    public static Guid ForResource(Guid? actorCompanyId, bool allowGlobal, Guid resourceCompanyId,
        Guid? selectedCompanyId, Func<string, Exception> error)
    {
        if (resourceCompanyId == Guid.Empty) throw error("The selected resource has no valid company.");
        if (selectedCompanyId.HasValue && selectedCompanyId != resourceCompanyId)
            throw error("The selected company does not match the resource company.");
        if (!allowGlobal && RequireOwnCompany(actorCompanyId, error) != resourceCompanyId)
            throw error("You cannot access a resource outside your authorized company.");
        return resourceCompanyId;
    }

    public static async Task<CompanySelectionOptions> OptionsAsync(ApplicationDbContext db,
        Guid? actorCompanyId, bool allowGlobal, Func<string, Exception> error, CancellationToken token = default)
    {
        if (!allowGlobal) return new() { CompanyId = RequireOwnCompany(actorCompanyId, error) };
        var companies = await db.Companies.AsNoTracking().Where(x => x.IsActive && !x.IsDeleted)
            .OrderBy(x => x.Name).ThenBy(x => x.Code)
            .Select(x => new CompanySelectionOption { Id = x.Id, Code = x.Code, Name = x.Name }).ToListAsync(token);
        var selected = companies.Any(x => x.Id == actorCompanyId) ? actorCompanyId
            : companies.Count == 1 ? companies[0].Id : (Guid?)null;
        return new() { CanSelectCompany = true, CompanyId = selected, Companies = companies };
    }

    private static Guid RequireOwnCompany(Guid? companyId, Func<string, Exception> error) =>
        companyId.HasValue && companyId != Guid.Empty ? companyId.Value : throw error("Current user is not assigned to a company.");
}
