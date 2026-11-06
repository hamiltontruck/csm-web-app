using System.Linq.Expressions;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Enums;
using CSM.Domain.Security;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Security;

/// <summary>Live, action-specific Officer scope. No assignment means own company;
/// an invalid active assignment never widens to company. Other roles retain their own authority.</summary>
internal sealed class ProcurementOfficerScope
{
    private User? EffectiveActor { get; init; }
    public User For(User original) => EffectiveActor ?? original;
    public bool IsScoped { get; private init; }
    public Guid CompanyId { get; private init; }
    public Guid[]? SiteIds { get; private init; }
    private Func<string, Exception> Error { get; init; } = message => new UnauthorizedAccessException(message);

    internal static async Task<ProcurementOfficerScope> ResolveAsync(ApplicationDbContext db,
        Guid actorId, GranularPermission permission, CancellationToken cancellationToken,
        Func<string, Exception> error, string[] independentRoles, bool allowOfficer = true)
    {
        // Check role identity even when inactive/deleted, so a stale Officer cannot
        // fall through to a permissive role-only legacy service path.
        var actor = await db.Users.IgnoreQueryFilters().AsNoTracking()
            .Include(x => x.UserRoles).ThenInclude(x => x.Role).ThenInclude(x => x.GranularPermissions)
            .SingleOrDefaultAsync(x => x.Id == actorId, cancellationToken);
        if (actor is null) throw error("Current user was not found.");
        var officer = actor.UserRoles.Any(x => x.Role.Name == AppRoles.ProcurementOfficer);
        if (!officer) return new();
        if (!actor.IsActive || actor.IsDeleted) throw error("Current user is inactive or unavailable.");
        var live = actor.UserRoles.Where(x => !x.IsDeleted && !x.Role.IsDeleted).ToArray();
        var independent = live.Where(x => x.Role.Name != AppRoles.ProcurementOfficer && independentRoles.Contains(x.Role.Name) &&
            RolePermissionRules.Allows(x.Role.Name, permission.ToString()) &&
            x.Role.GranularPermissions.Any(p => p.Permission == permission)).ToArray();
        if (independent.Length > 0)
        {
            // This is an untracked actor copy. Do not let a separate, ungranted
            // global role widen the independently granted role's original scope.
            actor.UserRoles = independent;
            return new() { EffectiveActor = actor };
        }
        if (!allowOfficer || !actor.CompanyId.HasValue || actor.CompanyId == Guid.Empty ||
            !RolePermissionRules.Allows(AppRoles.ProcurementOfficer, permission.ToString()) ||
            !live.Any(x => x.Role.Name == AppRoles.ProcurementOfficer && x.Role.GranularPermissions.Any(p => p.Permission == permission)))
            throw error("This action is not permitted for your account.");
        var company = actor.CompanyId.Value;
        var hasAssignment = await db.UserSiteAccesses.AsNoTracking()
            .AnyAsync(x => x.UserId == actorId && x.IsActive && !x.IsDeleted, cancellationToken);
        Guid[]? sites = null;
        if (hasAssignment)
            sites = await db.UserSiteAccesses.AsNoTracking()
                .Where(x => x.UserId == actorId && x.IsActive && !x.IsDeleted && x.CompanyId == company &&
                    x.Site.CompanyId == company && x.Site.IsActive && !x.Site.IsDeleted)
                .Select(x => x.SiteId).Distinct().ToArrayAsync(cancellationToken);
        actor.UserRoles = live.Where(x => x.Role.Name == AppRoles.ProcurementOfficer).ToArray();
        return new() { IsScoped = true, CompanyId = company, SiteIds = sites, Error = error, EffectiveActor = actor };
    }

    public void Ensure(Guid companyId, Guid? siteId = null, bool catalog = false)
    {
        if (IsScoped && (companyId != CompanyId || (!catalog && SiteIds is not null &&
            (!siteId.HasValue || !SiteIds.Contains(siteId.Value)))))
            throw Error("This record is outside your permitted company or sites.");
    }

    public IQueryable<T> Filter<T>(IQueryable<T> query, Expression<Func<T, Guid>> company,
        Expression<Func<T, Guid?>>? site = null)
    {
        if (!IsScoped) return query;
        var parameter = company.Parameters[0];
        Expression predicate = Expression.Equal(company.Body, Expression.Constant(CompanyId));
        if (SiteIds is not null && site is not null)
        {
            var body = new ReplaceParameter(site.Parameters[0], parameter).Visit(site.Body)!;
            var hasValue = Expression.Property(body, "HasValue");
            var contains = Expression.Call(typeof(Enumerable), nameof(Enumerable.Contains), [typeof(Guid)],
                Expression.Constant(SiteIds), Expression.Property(body, "Value"));
            predicate = Expression.AndAlso(predicate, Expression.AndAlso(hasValue, contains));
        }
        return query.Where(Expression.Lambda<Func<T, bool>>(predicate, parameter));
    }

    private sealed class ReplaceParameter(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : base.VisitParameter(node);
    }
}
