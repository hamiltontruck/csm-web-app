using CSM.Domain.Constants;
using CSM.Domain.Enums;
using CSM.Domain.Entities.Projects;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace CSM.Infrastructure.Services;

internal sealed class ProjectChildAccess
{
    private readonly ApplicationDbContext db;
    private readonly Guid? company;
    private readonly Guid actor;
    private readonly bool global;
    private readonly bool allSites;
    private ProjectChildAccess(ApplicationDbContext db, Guid actor, Guid? company, bool global, bool allSites)
        => (this.db, this.actor, this.company, this.global, this.allSites) = (db, actor, company, global, allSites);
    internal static async Task<ProjectChildAccess> ForAsync(ApplicationDbContext db, Guid actor, bool write, bool daily, CancellationToken ct)
    {
        if (actor == Guid.Empty) throw new UnauthorizedAccessException("Access denied.");
        var user = await db.Users.AsNoTracking().Include(x => x.UserRoles).ThenInclude(x => x.Role).ThenInclude(x => x.GranularPermissions)
            .SingleOrDefaultAsync(x => x.Id == actor && x.IsActive && !x.IsDeleted, ct)
            ?? throw new UnauthorizedAccessException("Access denied.");
        var roles = user.UserRoles.Where(x => !x.IsDeleted && !x.Role.IsDeleted).Select(x => x.Role).ToArray();
        bool Role(string name) => roles.Any(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
        bool Grant(GranularPermission p) => roles.Any(x => x.GranularPermissions.Any(g => g.Permission == p));
        var global = Role(AppRoles.SuperAdmin);
        var admin = Role(AppRoles.CompanyAdmin);
        if ((!global && (!user.CompanyId.HasValue || user.CompanyId == Guid.Empty)) ||
            !Grant(GranularPermission.Projects_View) ||
            (daily && !Grant(GranularPermission.Projects_DailyProgress_View)) ||
            (write && (!Grant(daily ? GranularPermission.Projects_DailyProgress_Create : GranularPermission.Projects_Manage) ||
                !(global || admin || Role(AppRoles.SiteManager) || (daily && Role(AppRoles.Supervisor))))))
            throw new UnauthorizedAccessException("Access denied.");
        return new(db, actor, user.CompanyId, global, global || admin);
    }
    internal IQueryable<Guid> Sites => db.ConstructionSites.Where(s => !s.IsDeleted && s.IsActive &&
        (global || s.CompanyId == company) && (allSites || db.UserSiteAccesses.Any(a =>
            a.UserId == actor && a.CompanyId == s.CompanyId && a.SiteId == s.Id && a.IsActive && !a.IsDeleted))).Select(s => s.Id);
    internal IQueryable<Project> Projects => db.Projects.Where(p => !p.IsDeleted && (global || p.CompanyId == company) &&
        db.ConstructionSites.Any(s => s.Id == p.ConstructionSiteId && s.CompanyId == p.CompanyId && !s.IsDeleted) && Sites.Contains(p.ConstructionSiteId));
    internal IQueryable<Guid> ProjectIds => Projects.Select(p => p.Id);
    internal IQueryable<DailySiteDiary> Diaries => db.DailySiteDiaries.Where(d => !d.IsDeleted && (global || d.CompanyId == company) &&
        Sites.Contains(d.SiteId) && db.ConstructionSites.Any(s => s.Id == d.SiteId && s.CompanyId == d.CompanyId) &&
        (!d.ProjectId.HasValue || Projects.Any(p => p.Id == d.ProjectId && p.CompanyId == d.CompanyId && p.ConstructionSiteId == d.SiteId)));
    internal async Task<Project> ProjectAsync(Guid id, CancellationToken ct) => await Projects.SingleOrDefaultAsync(p => p.Id == id, ct)
        ?? throw new UnauthorizedAccessException("Access denied.");
    internal async Task<Guid> DiaryParentAsync(Guid siteId, Guid? projectId, CancellationToken ct)
    {
        var site = await db.ConstructionSites.SingleOrDefaultAsync(s => s.Id == siteId && Sites.Contains(s.Id), ct)
            ?? throw new UnauthorizedAccessException("Access denied.");
        if (projectId.HasValue && (await ProjectAsync(projectId.Value, ct)).ConstructionSiteId != siteId)
            throw new ArgumentException("Project and site must match.");
        return site.CompanyId;
    }
    internal async Task<DailySiteDiary> DiaryAsync(Guid id, CancellationToken ct) => await Diaries.SingleOrDefaultAsync(d => d.Id == id, ct)
        ?? throw new UnauthorizedAccessException("Access denied.");
    internal async Task<DailyProgressLog> LogAsync(Guid id, CancellationToken ct) => await db.DailyProgressLogs.SingleOrDefaultAsync(l =>
        l.Id == id && !l.IsDeleted && Projects.Any(p => p.Id == l.ProjectId && p.CompanyId == l.CompanyId), ct)
        ?? throw new UnauthorizedAccessException("Access denied.");
    internal async Task<WbsTask> TaskAsync(Guid id, CancellationToken ct) => await db.WbsTasks.SingleOrDefaultAsync(t =>
        t.Id == id && !t.IsDeleted && Projects.Any(p => p.Id == t.ProjectId && p.CompanyId == t.CompanyId), ct)
        ?? throw new UnauthorizedAccessException("Access denied.");
}
