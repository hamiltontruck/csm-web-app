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
        var access = await ProjectChildAccess.ForAsync(_db, currentUserId, true, true, cancellationToken);
        var log = await access.LogAsync(request.DailyProgressLogId, cancellationToken);

        var task = await _db.WorkTasks
            .Include(x => x.ProjectPhase)
                .ThenInclude(x => x!.Tasks)
            .FirstOrDefaultAsync(x => x.Id == request.WorkTaskId && !x.IsDeleted && x.ProjectId == log.ProjectId && x.CompanyId == log.CompanyId, cancellationToken)
            ?? throw new ArgumentException("Invalid task.");

        if (task.ProjectPhase is not null && (task.ProjectPhase.IsDeleted || task.ProjectPhase.CompanyId != log.CompanyId || task.ProjectPhase.ProjectId != log.ProjectId))
            throw new ArgumentException("Invalid task phase.");
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
            phase.ProgressPercentage = phase.Tasks.Any(x => !x.IsDeleted && x.CompanyId == log.CompanyId && x.ProjectId == log.ProjectId)
                ? phase.Tasks.Where(x => !x.IsDeleted && x.CompanyId == log.CompanyId && x.ProjectId == log.ProjectId).Average(x => x.ProgressPercentage)
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
        var access = await ProjectChildAccess.ForAsync(_db, currentUserId, true, true, cancellationToken);
        var taskEntry = await _db.DailyProgressTaskEntries
            .FirstOrDefaultAsync(
                x => x.Id == request.DailyProgressTaskEntryId && !x.IsDeleted && _db.DailyProgressLogs.Any(l => l.Id == x.DailyProgressLogId && !l.IsDeleted && l.CompanyId == x.CompanyId && _db.WorkTasks.Any(t => t.Id == x.WorkTaskId && !t.IsDeleted && t.CompanyId == l.CompanyId && t.ProjectId == l.ProjectId) && access.Projects.Any(p => p.Id == l.ProjectId && p.CompanyId == l.CompanyId)),
                cancellationToken)
            ?? throw new UnauthorizedAccessException("Access denied.");

        if (!await _db.Equipment.AnyAsync(e => e.Id == request.EquipmentId && !e.IsDeleted && e.IsActive && e.CompanyId == taskEntry.CompanyId, cancellationToken))
            throw new ArgumentException("Invalid equipment.");
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
        var access = await ProjectChildAccess.ForAsync(_db, currentUserId, false, true, cancellationToken);
        var log = await access.LogAsync(dailyProgressLogId, cancellationToken);
        return await _db.DailyProgressTaskEntries
            .Where(x => x.DailyProgressLogId == dailyProgressLogId && !x.IsDeleted && x.CompanyId == log.CompanyId && !x.WorkTask.IsDeleted && x.WorkTask.CompanyId == log.CompanyId && x.WorkTask.ProjectId == log.ProjectId)
            .Include(x => x.WorkTask)
            .Include(x => x.EquipmentEntries)
            .Select(x => new DailyProgressTaskEntryResponse(
                x.Id,
                x.WorkTaskId,
                x.WorkTask.TaskNumber,
                x.WorkTask.Title,
                x.ProgressPercentage,
                x.LabourCount,
                x.EquipmentEntries.Count(e => !e.IsDeleted && e.CompanyId == log.CompanyId),
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
        var access = await ProjectChildAccess.ForAsync(_db, currentUserId, false, true, cancellationToken);
        var entry = await _db.DailyProgressTaskEntries.SingleOrDefaultAsync(x => x.Id == taskEntryId && !x.IsDeleted && _db.DailyProgressLogs.Any(l => l.Id == x.DailyProgressLogId && !l.IsDeleted && l.CompanyId == x.CompanyId && _db.WorkTasks.Any(t => t.Id == x.WorkTaskId && !t.IsDeleted && t.CompanyId == l.CompanyId && t.ProjectId == l.ProjectId) && access.Projects.Any(p => p.Id == l.ProjectId && p.CompanyId == l.CompanyId)), cancellationToken) ?? throw new UnauthorizedAccessException("Access denied.");
        return await _db.DailyProgressEquipmentEntries
            .Where(x => x.DailyProgressTaskEntryId == taskEntryId && !x.IsDeleted && x.CompanyId == entry.CompanyId)
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
