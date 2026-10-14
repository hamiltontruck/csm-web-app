using CSM.Application.Procurement.EquipmentHourLogs;
using CSM.Domain.Entities.Equipment;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public class EquipmentHourLogService
{
    private readonly ApplicationDbContext _dbContext;

    public EquipmentHourLogService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<EquipmentHourLogResponse>> GetAllAsync(
        Guid companyId,
        Guid? equipmentId,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.EquipmentHourLogs
            .AsNoTracking()
            .Include(x => x.Equipment)
            .Where(x => x.CompanyId == companyId);

        if (equipmentId.HasValue)
        {
            query = query.Where(x => x.EquipmentId == equipmentId.Value);
        }

        return await query
            .OrderByDescending(x => x.LoggedAtUtc)
            .Select(x => new EquipmentHourLogResponse(
                x.Id,
                x.EquipmentId,
                x.Equipment.EquipmentCode,
                x.Equipment.Name,

                x.LoggedAtUtc,

                x.PreviousMeterReading,
                x.CurrentMeterReading,
                x.HoursWorked,

                x.OperatorEmployeeId,
                x.OperatorName,

                x.ProjectId,
                x.ProjectName,

                x.Activity,
                x.Remarks
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task<EquipmentHourLogResponse> CreateAsync(
        Guid companyId,
        CreateEquipmentHourLogRequest request,
        CancellationToken cancellationToken = default)
    {
        var equipment = await _dbContext.Equipment
            .FirstOrDefaultAsync(
                x => x.Id == request.EquipmentId,
                cancellationToken);

        if (equipment == null)
        {
            throw new InvalidOperationException(
                $"Equipment not found. EquipmentId={request.EquipmentId}");
        }
        Console.WriteLine($"CompanyId : {companyId}");
        Console.WriteLine($"SiteId    : {request.SiteId}");
        Console.WriteLine($"EquipId   : {request.EquipmentId}");

        var site = await _dbContext.ConstructionSites
            .SingleOrDefaultAsync(
                x => x.Id == request.SiteId &&
                     x.CompanyId == equipment.CompanyId,
                cancellationToken);

        if (site == null)
        {
            throw new InvalidOperationException("Construction site was not found.");
        }

        var hoursWorked = request.CurrentMeter - request.PreviousMeter;

        if (hoursWorked < 0)
        {
            throw new InvalidOperationException(
                "Current meter cannot be less than previous meter.");
        }

        var entity = new EquipmentHourLog
        {
            CompanyId = companyId,
            EquipmentId = equipment.Id,

            ProjectId = site.Id,
            ProjectName = site.Name,
            LoggedAtUtc = request.LogDateUtc,

            OperatorName = request.OperatorName,
            Activity = request.Activity,

            PreviousMeterReading = request.PreviousMeter,
            CurrentMeterReading = request.CurrentMeter,
            HoursWorked = hoursWorked,

            Remarks = request.Remarks
        };

        _dbContext.EquipmentHourLogs.Add(entity);

        // Update current equipment meter
        equipment.CurrentMeterReading = request.CurrentMeter;

        await _dbContext.SaveChangesAsync(cancellationToken);

        entity.Equipment = equipment;

        return Map(entity);
    }

    private static EquipmentHourLogResponse Map(EquipmentHourLog log)
    {
        return new EquipmentHourLogResponse(
            log.Id,
            log.EquipmentId,
            log.Equipment.EquipmentCode,
            log.Equipment.Name,

            log.LoggedAtUtc,

            log.PreviousMeterReading,
            log.CurrentMeterReading,
            log.HoursWorked,

            log.OperatorEmployeeId,
            log.OperatorName,

            log.ProjectId,
            log.ProjectName,

            log.Activity,
            log.Remarks
        );
    }
}