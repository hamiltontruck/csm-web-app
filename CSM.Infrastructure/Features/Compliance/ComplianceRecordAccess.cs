using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Features.Compliance;

internal static class ComplianceRecordAccess
{
    internal static async Task<Guid> CompanyAsync(ApplicationDbContext db, Guid actorId, bool write, CancellationToken ct)
    {
        var company = await db.Users.AsNoTracking()
            .Where(x => x.Id == actorId && x.IsActive && !x.IsDeleted)
            .Select(x => x.CompanyId).SingleOrDefaultAsync(ct);
        var permissions = new PermissionEvaluator(db);
        if (!company.HasValue || company.Value == Guid.Empty ||
            !await permissions.HasPermissionAsync(actorId, "Compliance_View", ct) ||
            (write && !await permissions.HasPermissionAsync(actorId, "Compliance_Manage", ct)))
            throw new UnauthorizedAccessException("Compliance access is not permitted.");
        return company.Value;
    }

    internal static async Task ParentsAsync(ApplicationDbContext db, Guid companyId, Guid siteId, Guid? projectId, Guid? employeeId, CancellationToken ct)
    {
        if (!await db.ConstructionSites.AnyAsync(x => x.Id == siteId && x.CompanyId == companyId && !x.IsDeleted, ct))
            throw new KeyNotFoundException("Construction site was not found.");
        if (projectId.HasValue && !await db.Projects.AnyAsync(x => x.Id == projectId && x.CompanyId == companyId && x.ConstructionSiteId == siteId && !x.IsDeleted, ct))
            throw new KeyNotFoundException("Project was not found for the selected site.");
        if (employeeId.HasValue && !await db.Employees.AnyAsync(x => x.Id == employeeId && x.CompanyId == companyId && !x.IsDeleted, ct))
            throw new KeyNotFoundException("Employee was not found.");
    }
}
