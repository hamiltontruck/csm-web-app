using CSM.Application.Compliance.Alerts;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public class ComplianceAlertService
{
    private readonly ApplicationDbContext _db;

    public ComplianceAlertService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<ComplianceAlertResponse>> GetAsync(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var limit = today.AddDays(30);

        return await _db.Permits
            .AsNoTracking()
            .Include(x => x.ConstructionSite)
            .Where(x =>
                x.CompanyId == companyId &&
                x.Status == PermitStatus.Active &&
                x.ExpiryDate != null &&
                x.ExpiryDate <= limit)
            .OrderBy(x => x.ExpiryDate)
            .Select(x => new ComplianceAlertResponse(
                x.Id,
                x.PermitNumber,
                x.ConstructionSite.Name,
                x.Name,
                x.ExpiryDate!.Value,
                x.ExpiryDate.Value.DayNumber - today.DayNumber,
                x.ExpiryDate.Value < today
            ))
            .ToListAsync(cancellationToken);
    }
}