using CSM.Application.Common.Exceptions;
using CSM.Application.Procurement.EquipmentTimesheets;
using CSM.Domain.Entities.Equipment;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public sealed class EquipmentTimesheetService
{
    private readonly ApplicationDbContext _db;

    public EquipmentTimesheetService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<EquipmentTimesheetResponse>> GetAllAsync(
        Guid companyId,
        Guid? equipmentId,
        CancellationToken cancellationToken = default)
    {
        var query = _db.EquipmentTimesheets
            .AsNoTracking()
            .Include(x => x.Equipment)
            .Where(x => x.CompanyId == companyId);

        if (equipmentId.HasValue)
            query = query.Where(x => x.EquipmentId == equipmentId.Value);

        return await query
            .OrderByDescending(x => x.WorkDate)
            .Select(x => new EquipmentTimesheetResponse(
                x.Id,
                x.EquipmentId,
                x.Equipment.EquipmentCode,
                x.Equipment.Name,
                x.SiteId,
                "",
                x.WorkDate.ToDateTime(TimeOnly.MinValue),
                x.OperatorName,
                x.Shift,
                x.StartTime.ToTimeSpan(),
                x.EndTime.ToTimeSpan(),
                x.BreakMinutes,
                x.WorkedHours,
                x.Activity,
                x.Remarks))
            .ToListAsync(cancellationToken);
    }

    public async Task<EquipmentTimesheetResponse> CreateAsync(
        Guid companyId,
        CreateEquipmentTimesheetRequest request,
        CancellationToken cancellationToken = default)
    {
        var duration = request.EndTime - request.StartTime;

        if (duration < TimeSpan.Zero)
            duration = duration.Add(TimeSpan.FromDays(1));

        var workedHours = (decimal)duration.TotalHours
                        - (request.BreakMinutes / 60m);

        if (workedHours < 0)
            workedHours = 0;

        var entity = new EquipmentTimesheet
        {
            CompanyId = companyId,
            EquipmentId = request.EquipmentId,
            SiteId = request.SiteId,
            WorkDate = DateOnly.FromDateTime(request.WorkDate),
            OperatorName = request.OperatorName,
            Shift = request.Shift,
            StartTime = TimeOnly.FromTimeSpan(request.StartTime),
            EndTime = TimeOnly.FromTimeSpan(request.EndTime),
            BreakMinutes = request.BreakMinutes,
            WorkedHours = Math.Round(workedHours, 2),
            Activity = request.Activity,
            Remarks = request.Remarks
        };

        _db.EquipmentTimesheets.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        var equipment = await _db.Equipment
            .FirstAsync(x => x.Id == entity.EquipmentId, cancellationToken);

        return new EquipmentTimesheetResponse(
            entity.Id,
            entity.EquipmentId,
            equipment.EquipmentCode,
            equipment.Name,
            entity.SiteId,
            "",
            entity.WorkDate.ToDateTime(TimeOnly.MinValue),
            entity.OperatorName,
            entity.Shift,
            entity.StartTime.ToTimeSpan(),
            entity.EndTime.ToTimeSpan(),
            entity.BreakMinutes,
            entity.WorkedHours,
            entity.Activity,
            entity.Remarks);
    }

    public async Task<EquipmentTimesheetResponse> GetByIdAsync(
        Guid companyId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var item = await _db.EquipmentTimesheets
            .AsNoTracking()
            .Include(x => x.Equipment)
            .FirstOrDefaultAsync(
                x => x.CompanyId == companyId && x.Id == id,
                cancellationToken);

        if (item is null)
            throw new EquipmentManagementException("Timesheet not found.");

        return new EquipmentTimesheetResponse(
            item.Id,
            item.EquipmentId,
            item.Equipment.EquipmentCode,
            item.Equipment.Name,
            item.SiteId,
            "",
            item.WorkDate.ToDateTime(TimeOnly.MinValue),
            item.OperatorName,
            item.Shift,
            item.StartTime.ToTimeSpan(),
            item.EndTime.ToTimeSpan(),
            item.BreakMinutes,
            item.WorkedHours,
            item.Activity,
            item.Remarks);
    }

    public async Task<EquipmentTimesheetResponse> UpdateAsync(
        Guid companyId,
        Guid id,
        CreateEquipmentTimesheetRequest request,
        CancellationToken cancellationToken = default)
    {
        var item = await _db.EquipmentTimesheets
            .Include(x => x.Equipment)
            .FirstOrDefaultAsync(
                x => x.CompanyId == companyId && x.Id == id,
                cancellationToken);

        if (item is null)
            throw new EquipmentManagementException("Timesheet not found.");

        var duration = request.EndTime - request.StartTime;

        if (duration < TimeSpan.Zero)
            duration = duration.Add(TimeSpan.FromDays(1));

        var workedHours = (decimal)duration.TotalHours
                        - (request.BreakMinutes / 60m);

        if (workedHours < 0)
            workedHours = 0;

        item.WorkedHours = Math.Round(workedHours, 2);

        Console.WriteLine($"Duration={duration} Worked={item.WorkedHours}");

        item.EquipmentId = request.EquipmentId;
        item.SiteId = request.SiteId;
        item.WorkDate = DateOnly.FromDateTime(request.WorkDate);
        item.OperatorName = request.OperatorName;
        item.Shift = request.Shift;
        item.StartTime = TimeOnly.FromTimeSpan(request.StartTime);
        item.EndTime = TimeOnly.FromTimeSpan(request.EndTime);
        item.BreakMinutes = request.BreakMinutes;
        item.Activity = request.Activity;
        item.Remarks = request.Remarks;

        item.WorkedHours = Math.Round(workedHours, 2);

        Console.WriteLine(
            $"Duration={duration} Worked={item.WorkedHours}");

        await _db.SaveChangesAsync(cancellationToken);

        return new EquipmentTimesheetResponse(
            item.Id,
            item.EquipmentId,
            item.Equipment.EquipmentCode,
            item.Equipment.Name,
            item.SiteId,
            "",
            item.WorkDate.ToDateTime(TimeOnly.MinValue),
            item.OperatorName,
            item.Shift,
            item.StartTime.ToTimeSpan(),
            item.EndTime.ToTimeSpan(),
            item.BreakMinutes,
            item.WorkedHours,
            item.Activity,
            item.Remarks);
    }
}