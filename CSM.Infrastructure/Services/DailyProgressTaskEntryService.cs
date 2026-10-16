using CSM.Application.Common.Exceptions;
using CSM.Application.Projects.DailyProgressTaskEntries;
using CSM.Application.Projects.DailyProgressTaskEntries.Dtos;
using CSM.Domain.Entities.Projects;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public class DailyProgressTaskEntryService : IDailyProgressTaskEntryService
{
    private readonly ApplicationDbContext _db;

    public DailyProgressTaskEntryService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<DailyProgressTaskEntryResponse> CreateAsync(
        Guid currentUserId,
        CreateDailyProgressTaskEntryRequest request,
        CancellationToken cancellationToken = default)
    {
        var log = await _db.DailyProgressLogs
            .FirstOrDefaultAsync(x => x.Id == request.DailyProgressLogId, cancellationToken)
            ?? throw new DailyProgressLogManagementException("Daily log not found.");

        var task = await _db.WorkTasks
            .Include(x => x.ProjectPhase)
                .ThenInclude(x => x!.Tasks)
            .FirstOrDefaultAsync(x => x.Id == request.WorkTaskId, cancellationToken)
            ?? throw new DailyProgressLogManagementException("Task not found.");

        var entry = new DailyProgressTaskEntry
        {
            CompanyId = log.CompanyId,
            DailyProgressLogId = log.Id,
            WorkTaskId = task.Id,
            ProgressPercentage = request.ProgressPercentage,
            LabourCount = request.LabourCount,
            EquipmentHours = request.EquipmentHours,
            WorkCompleted = request.WorkCompleted,
            Remarks = request.Remarks
        };

        _db.DailyProgressTaskEntries.Add(entry);

        // Update task
        task.ProgressPercentage = request.ProgressPercentage;
        task.Status = request.ProgressPercentage switch
        {
            0 => WorkTaskStatus.NotStarted,
            100 => WorkTaskStatus.Completed,
            _ => WorkTaskStatus.InProgress
        };

        // Update phase
        if (task.ProjectPhase is { } phase)
        {
            phase.ProgressPercentage = phase.Tasks.Any()
                ? phase.Tasks.Average(x => x.ProgressPercentage)
                : 0;

            phase.Status = phase.ProgressPercentage switch
            {
                0 => ProjectPhaseStatus.NotStarted,
                100 => ProjectPhaseStatus.Completed,
                _ => ProjectPhaseStatus.InProgress
            };
        }

        await _db.SaveChangesAsync(cancellationToken);

        return new DailyProgressTaskEntryResponse(
            entry.Id,
            task.Id,
            task.TaskNumber,
            task.Title,
            entry.ProgressPercentage,
            entry.LabourCount,
            0,                          // EquipmentCount
            entry.EquipmentHours,
            entry.WorkCompleted,
            entry.Remarks);
    }

    public async Task<EquipmentEntryResponse> AddEquipmentAsync(
        Guid currentUserId,
        CreateEquipmentEntryRequest request,
        CancellationToken cancellationToken = default)
    {
        var taskEntry = await _db.DailyProgressTaskEntries
            .FirstOrDefaultAsync(
                x => x.Id == request.DailyProgressTaskEntryId,
                cancellationToken)
            ?? throw new DailyProgressLogManagementException("Task entry not found.");

        var equipment = new DailyProgressEquipmentEntry
        {
            CompanyId = taskEntry.CompanyId,
            DailyProgressTaskEntryId = taskEntry.Id,
            EquipmentId = request.EquipmentId,
            EquipmentName = request.EquipmentName,
            HoursUsed = request.HoursUsed,
            OperatorName = request.OperatorName,
            Remarks = request.Remarks
        };

        _db.DailyProgressEquipmentEntries.Add(equipment);

        // Update total equipment hours on the task entry
        taskEntry.EquipmentHours += request.HoursUsed;

        await _db.SaveChangesAsync(cancellationToken);

        return new EquipmentEntryResponse(
            equipment.Id,
            equipment.EquipmentId,
            equipment.EquipmentName,
            equipment.HoursUsed,
            equipment.OperatorName,
            equipment.Remarks);
    }

    public async Task<IReadOnlyCollection<DailyProgressTaskEntryResponse>> GetByLogAsync(
        Guid currentUserId,
        Guid dailyProgressLogId,
        CancellationToken cancellationToken = default)
    {
        return await _db.DailyProgressTaskEntries
            .Where(x => x.DailyProgressLogId == dailyProgressLogId)
            .Include(x => x.WorkTask)
            .Include(x => x.EquipmentEntries)
            .Select(x => new DailyProgressTaskEntryResponse(
                x.Id,
                x.WorkTaskId,
                x.WorkTask.TaskNumber,
                x.WorkTask.Title,
                x.ProgressPercentage,
                x.LabourCount,
                x.EquipmentEntries.Count,
                x.EquipmentHours,
                x.WorkCompleted,
                x.Remarks))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EquipmentEntryResponse>> GetEquipmentAsync(
        Guid currentUserId,
        Guid taskEntryId,
        CancellationToken cancellationToken = default)
    {
        return await _db.DailyProgressEquipmentEntries
            .Where(x => x.DailyProgressTaskEntryId == taskEntryId)
            .Select(x => new EquipmentEntryResponse(
                x.Id,
                x.EquipmentId,
                x.EquipmentName,
                x.HoursUsed,
                x.OperatorName,
                x.Remarks))
            .ToListAsync(cancellationToken);
    }
}
