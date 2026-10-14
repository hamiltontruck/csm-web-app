using CSM.Application.Procurement.EquipmentUtilization;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public class EquipmentUtilizationService
{
    private readonly ApplicationDbContext _dbContext;

    public EquipmentUtilizationService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<EquipmentUtilizationResponse>> GetAllAsync(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var equipment = await _dbContext.Equipment
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .ToListAsync(cancellationToken);

        var assignments = await _dbContext.EquipmentAssignments
            .AsNoTracking()
            .Include(x => x.ConstructionSite)
            .Where(x => x.CompanyId == companyId)
            .ToListAsync(cancellationToken);

        var hourLogs = await _dbContext.EquipmentHourLogs
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .ToListAsync(cancellationToken);

        var fuelLogs = await _dbContext.EquipmentFuelLogs
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .ToListAsync(cancellationToken);

        var downtime = await _dbContext.EquipmentDowntimeRecords
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .ToListAsync(cancellationToken);

        var maintenance = await _dbContext.EquipmentMaintenanceRecords
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .ToListAsync(cancellationToken);

        var result = equipment.Select(e =>
        {
            var assignment = assignments
                .Where(x =>
                    x.EquipmentId == e.Id &&
                    x.ReleasedAtUtc == null)
                .OrderByDescending(x => x.AssignedAtUtc)
                .FirstOrDefault();

            var hours = hourLogs
                .Where(x => x.EquipmentId == e.Id)
                .Sum(x => x.HoursWorked);

            var fuel = fuelLogs
                .Where(x => x.EquipmentId == e.Id)
                .Sum(x => x.Liters);

            var down = downtime
                .Where(x => x.EquipmentId == e.Id)
                .Sum(x =>
                {
                    var end = x.EndedAtUtc ?? DateTime.UtcNow;

                    return (decimal)(end - x.StartedAtUtc)
                        .TotalHours;
                });

            var cost = maintenance
                .Where(x => x.EquipmentId == e.Id)
                .Sum(x => x.Cost ?? 0);

            return new EquipmentUtilizationResponse(
                e.Id,
                e.EquipmentCode,
                e.Name,
                assignment?.ConstructionSiteId,
                assignment?.ConstructionSite?.Name,
                hours,
                fuel,
                down,
                cost);
        }).ToList();

        return result;
    }
}