using CSM.Domain.Common;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

internal static class FinanceApprovalPolicy
{
    private static string? ProvenanceBlock(AuditableEntity record, Guid actorId) =>
        record.CreatedBy is null || record.CreatedBy == Guid.Empty
            ? "Creator information is missing. Create a new record to request approval."
            : record.CreatedBy == actorId || record.UpdatedBy == actorId
                ? "You cannot approve a record you created or last edited. Ask another authorized person to approve it."
                : null;

    public static void EnsureIndependent(AuditableEntity record, Guid actorId, Func<string, Exception> error)
    {
        if (ProvenanceBlock(record, actorId) is string reason) throw error(reason);
    }

    public static async Task<string?> GetBlockReasonAsync(ApplicationDbContext db, Guid actorId,
        AuditableEntity record, Guid companyId, Guid? siteId, GranularPermission permission,
        bool awaitingApproval, CancellationToken ct)
    {
        if (!awaitingApproval) return "This record is not awaiting approval.";
        if (ProvenanceBlock(record, actorId) is string reason) return reason;
        if (!await new PermissionEvaluator(db).HasPermissionAsync(actorId, permission.ToString(), ct))
            return "Approval permission is not available for your account.";
        User? actor;
        try
        {
            actor = await FinanceOfficerScope.ResolveActorAsync(db, actorId, permission, ct,
                message => new UnauthorizedAccessException(message), ["SuperAdmin", "CompanyAdmin", "Accountant"]);
        }
        catch (UnauthorizedAccessException) { return "Approval is outside your current scope."; }
        actor ??= await db.Users.AsNoTracking().Include(x => x.UserRoles).ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(x => x.Id == actorId && x.IsActive && !x.IsDeleted, ct);
        var roles = actor?.UserRoles.Where(x => !x.IsDeleted && !x.Role.IsDeleted).Select(x => x.Role.Name).ToArray() ?? [];
        if (actor is null || !roles.Any(x => new[] { "SuperAdmin", "CompanyAdmin", "Accountant", "FinanceOfficer" }.Contains(x, StringComparer.OrdinalIgnoreCase)) ||
            (!roles.Contains("SuperAdmin", StringComparer.OrdinalIgnoreCase) && actor.CompanyId != companyId))
            return "Approval is outside your current scope.";
        if (SiteFinanceScope.IsSiteLimited(actor) && !await SiteFinanceScope.HasSiteAsync(db, actor, siteId, ct))
            return "Approval is outside your assigned site.";
        return null;
    }
}
