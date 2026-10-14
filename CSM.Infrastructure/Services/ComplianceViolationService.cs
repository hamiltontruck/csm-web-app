using CSM.Application.Compliance.Violations;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public class ComplianceViolationService
{
    private readonly ApplicationDbContext _db;

    public ComplianceViolationService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<ViolationResponse>> GetAllAsync(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        return await _db.InspectionChecklistItems
            .AsNoTracking()
            .Include(x => x.Inspection)
            .ThenInclude(x => x.ConstructionSite)
            .Where(x =>
                x.CompanyId == companyId &&
                !x.IsResolved)
            .OrderBy(x => x.DueDate)
            .Select(x => new ViolationResponse(
                x.Id,
                x.Inspection.InspectionNumber,
                x.Inspection.ConstructionSite.Name,
                x.Requirement,
                x.Observation,
                x.CorrectiveAction,
                x.IsResolved ? "Resolved" : "Open",
                x.DueDate,
                x.IsResolved
            ))
            .ToListAsync(cancellationToken);
    }
}