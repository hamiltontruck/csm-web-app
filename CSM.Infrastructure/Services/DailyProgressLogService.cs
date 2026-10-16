using CSM.Application.Common.Exceptions;
using CSM.Application.Projects.DailyProgress;
using CSM.Application.Projects.DailyProgress.Dtos;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Projects;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public sealed class DailyProgressLogService : IDailyProgressLogService
{
    private readonly ApplicationDbContext _dbContext;

    public DailyProgressLogService(
        ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<DailyProgressLogResponse>> GetAllAsync(
        Guid currentUserId,
        Guid projectId,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        var project = await GetProjectAsync(
            projectId,
            cancellationToken);

        EnsureProjectAccess(actor, project);

        if (fromDate.HasValue &&
            toDate.HasValue &&
            toDate.Value < fromDate.Value)
        {
            throw new DailyProgressLogManagementException(
                "To date cannot be earlier than from date.");
        }

        IQueryable<DailyProgressLog> query =
            _dbContext.DailyProgressLogs
                .AsNoTracking()
                .Include(x => x.DailyProgressTaskEntries)
                    .ThenInclude(x => x.EquipmentEntries)
                .Where(x => x.ProjectId == project.Id);

        if (fromDate.HasValue)
        {
            query = query.Where(
                x => x.LogDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(
                x => x.LogDate <= toDate.Value);
        }

        var logs = await query
            .OrderByDescending(x => x.LogDate)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return logs
            .Select(Map)
            .ToArray();
    }

    public async Task<DailyProgressLogResponse> GetByIdAsync(
        Guid currentUserId,
        Guid logId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        var log = await GetLogAsync(
            logId,
            cancellationToken);

        var project = await GetProjectAsync(
            log.ProjectId,
            cancellationToken);

        EnsureProjectAccess(actor, project);

        return Map(log);
    }

    public async Task<DailyProgressLogResponse> CreateAsync(
        Guid currentUserId,
        CreateDailyProgressLogRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanModify(actor);

        ValidateInput(
            request.LogDate,
            request.TemperatureCelsius,
            request.LaborCount,
            request.ContractorLaborCount,
            request.ProgressPercentage);

        var project = await GetProjectAsync(
            request.ProjectId,
            cancellationToken);

        EnsureProjectAccess(actor, project);
        EnsureProjectAllowsDailyReporting(project);

        var duplicateExists =
            await _dbContext.DailyProgressLogs
                .AnyAsync(
                    x =>
                        x.ProjectId == project.Id &&
                        x.LogDate == request.LogDate,
                    cancellationToken);

        var existing = await _dbContext.DailyProgressLogs
            .FirstOrDefaultAsync(
                x => x.ProjectId == project.Id &&
                     x.LogDate == request.LogDate,
                cancellationToken);

        if (existing is not null)
        {
            return Map(existing);
        }

        var logNumber =
            await GetNextLogNumberAsync(
                project.CompanyId,
                cancellationToken);

        var log = new DailyProgressLog
        {
            CompanyId = project.CompanyId,
            LogNumber = logNumber,
            ProjectId = project.Id,
            LogDate = request.LogDate,
            WeatherCondition =
                Clean(request.WeatherCondition),
            TemperatureCelsius =
                request.TemperatureCelsius,
            LaborCount =
                request.LaborCount,
            ContractorLaborCount =
                request.ContractorLaborCount,
            ProgressPercentage =
                request.ProgressPercentage,
            WorkCompleted =
                Clean(request.WorkCompleted),
            WorkPlannedNext =
                Clean(request.WorkPlannedNext),
            MaterialsUsedSummary =
                Clean(request.MaterialsUsedSummary),
            EquipmentUsedSummary =
                Clean(request.EquipmentUsedSummary),
            Delays =
                Clean(request.Delays),
            Issues =
                Clean(request.Issues),
            SafetyNotes =
                Clean(request.SafetyNotes),
            Remarks =
                Clean(request.Remarks)
        };

        var executionStrategy =
            _dbContext.Database.CreateExecutionStrategy();

        await executionStrategy.ExecuteAsync(
            async () =>
            {
                await using var transaction =
                    await _dbContext.Database
                        .BeginTransactionAsync(
                            System.Data.IsolationLevel.Serializable,
                            cancellationToken);

                _dbContext.DailyProgressLogs.Add(log);

                // Load the task for this daily report
                var task = await _dbContext.WorkTasks
                    .Include(t => t.ProjectPhase)
                        .ThenInclude(p => p!.Tasks)
                    .FirstOrDefaultAsync(
                        t => t.Id == request.TaskId,
                        cancellationToken);

                if (task is not null)
                {
                    // Update task progress
                    task.ProgressPercentage = request.ProgressPercentage;

                    task.Status = request.ProgressPercentage switch
                    {
                        0 => WorkTaskStatus.NotStarted,
                        100 => WorkTaskStatus.Completed,
                        _ => WorkTaskStatus.InProgress
                    };

                    // Recalculate phase progress
                    if (task.ProjectPhase is { } phase)
                    {
                        phase.ProgressPercentage =
                            phase.Tasks.Any()
                                ? phase.Tasks.Average(x => x.ProgressPercentage)
                                : 0;

                        phase.Status = phase.ProgressPercentage switch
                        {
                            0 => ProjectPhaseStatus.NotStarted,
                            100 => ProjectPhaseStatus.Completed,
                            _ => ProjectPhaseStatus.InProgress
                        };
                    }
                }

                await _dbContext.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);
            });

        return Map(log);
    }

    public async Task<DailyProgressLogResponse> UpdateAsync(
        Guid currentUserId,
        Guid logId,
        UpdateDailyProgressLogRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanModify(actor);

        var log = await GetLogAsync(
            logId,
            cancellationToken);

        var project = await GetProjectAsync(
            log.ProjectId,
            cancellationToken);

        EnsureProjectAccess(actor, project);
        EnsureProjectAllowsDailyReporting(project);

        ValidateInput(
            log.LogDate,
            request.TemperatureCelsius,
            request.LaborCount,
            request.ContractorLaborCount,
            request.ProgressPercentage);

        log.WeatherCondition =
            Clean(request.WeatherCondition);

        log.TemperatureCelsius =
            request.TemperatureCelsius;

        log.LaborCount =
            request.LaborCount;

        log.ContractorLaborCount =
            request.ContractorLaborCount;

        log.ProgressPercentage =
            request.ProgressPercentage;

        log.WorkCompleted =
            Clean(request.WorkCompleted);

        log.WorkPlannedNext =
            Clean(request.WorkPlannedNext);

        log.MaterialsUsedSummary =
            Clean(request.MaterialsUsedSummary);

        log.EquipmentUsedSummary =
            Clean(request.EquipmentUsedSummary);

        log.Delays =
            Clean(request.Delays);

        log.Issues =
            Clean(request.Issues);

        log.SafetyNotes =
            Clean(request.SafetyNotes);

        log.Remarks =
            Clean(request.Remarks);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(log);
    }

    private async Task<User> GetActorAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(
                x => x.Id == userId,
                cancellationToken);

        if (user is null || !user.IsActive)
        {
            throw new DailyProgressLogManagementException(
                "Current user was not found or is inactive.");
        }

        return user;
    }

    private async Task<Project> GetProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Projects
            .SingleOrDefaultAsync(
                x => x.Id == projectId,
                cancellationToken)
            ?? throw new DailyProgressLogManagementException(
                "Project was not found.");
    }

    private async Task<DailyProgressLog> GetLogAsync(
        Guid logId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.DailyProgressLogs
            .SingleOrDefaultAsync(
                x => x.Id == logId,
                cancellationToken)
            ?? throw new DailyProgressLogManagementException(
                "Daily progress log was not found.");
    }

    private static void EnsureProjectAccess(
        User actor,
        Project project)
    {
        if (IsSuperAdmin(actor))
        {
            return;
        }

        EnsureReadRole(actor);

        if (!actor.CompanyId.HasValue ||
            actor.CompanyId.Value != project.CompanyId)
        {
            throw new DailyProgressLogManagementException(
                "You cannot access daily progress logs outside your company.");
        }
    }

    private static void EnsureCanModify(User actor)
    {
        if (IsSuperAdmin(actor))
        {
            return;
        }

        if (HasRole(actor, AppRoles.CompanyAdmin) ||
            HasRole(actor, AppRoles.SiteManager) ||
            HasRole(actor, AppRoles.Supervisor))
        {
            return;
        }

        throw new DailyProgressLogManagementException(
            "You are not authorized to create or modify daily progress logs.");
    }

    private static void EnsureReadRole(User actor)
    {
        if (HasRole(actor, AppRoles.CompanyAdmin) ||
            HasRole(actor, AppRoles.SiteManager) ||
            HasRole(actor, AppRoles.Supervisor))
        {
            return;
        }

        throw new DailyProgressLogManagementException(
            "You are not authorized to access daily progress logs.");
    }

    private static void EnsureProjectAllowsDailyReporting(
        Project project)
    {
        if (project.Status is
            ProjectStatus.Completed or
            ProjectStatus.Cancelled or
            ProjectStatus.Closed)
        {
            throw new DailyProgressLogManagementException(
                $"Daily progress logs cannot be modified for a {project.Status} project.");
        }
    }

    private static void ValidateInput(
        DateOnly logDate,
        decimal? temperatureCelsius,
        int laborCount,
        int contractorLaborCount,
        decimal progressPercentage)
    {
        var today = DateOnly.FromDateTime(
            DateTime.UtcNow);

        if (logDate > today)
        {
            throw new DailyProgressLogManagementException(
                "Daily progress log date cannot be in the future.");
        }

        if (laborCount < 0)
        {
            throw new DailyProgressLogManagementException(
                "Labor count cannot be negative.");
        }

        if (contractorLaborCount < 0)
        {
            throw new DailyProgressLogManagementException(
                "Contractor labor count cannot be negative.");
        }

        if (progressPercentage < 0m ||
            progressPercentage > 100m)
        {
            throw new DailyProgressLogManagementException(
                "Progress percentage must be between 0 and 100.");
        }

        if (temperatureCelsius.HasValue &&
            (temperatureCelsius.Value < -60m ||
             temperatureCelsius.Value > 70m))
        {
            throw new DailyProgressLogManagementException(
                "Temperature is outside the supported range.");
        }
    }

    private static bool IsSuperAdmin(User user)
    {
        return HasRole(
            user,
            AppRoles.SuperAdmin);
    }

    private static bool HasRole(
        User user,
        string roleName)
    {
        return user.UserRoles.Any(
            x =>
                !x.IsDeleted &&
                !x.Role.IsDeleted &&
                string.Equals(
                    x.Role.Name,
                    roleName,
                    StringComparison.OrdinalIgnoreCase));
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private async Task<string> GetNextLogNumberAsync(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var lastLogNumber =
            await _dbContext.DailyProgressLogs
                .Where(x => x.CompanyId == companyId)
                .OrderByDescending(x => x.LogNumber)
                .Select(x => x.LogNumber)
                .FirstOrDefaultAsync(
                    cancellationToken);

        var nextNumber = 1;

        if (!string.IsNullOrWhiteSpace(lastLogNumber) &&
            lastLogNumber.StartsWith(
                "DPL-",
                StringComparison.OrdinalIgnoreCase))
        {
            var numericPart =
                lastLogNumber.Substring(4);

            if (int.TryParse(
                    numericPart,
                    out var currentNumber))
            {
                nextNumber = currentNumber + 1;
            }
        }

        return $"DPL-{nextNumber:D6}";
    }

    private static DailyProgressLogResponse Map(DailyProgressLog log)
    {
        return new DailyProgressLogResponse(
            log.Id,
            log.LogNumber,
            log.CompanyId,
            log.ProjectId,
            log.LogDate,
            log.WeatherCondition,
            log.TemperatureCelsius,

            // Existing fields
            log.DailyProgressTaskEntries.Sum(x => x.LabourCount),
            log.ContractorLaborCount,
            log.ProgressPercentage,

            log.WorkCompleted,
            log.WorkPlannedNext,
            log.MaterialsUsedSummary,
            log.EquipmentUsedSummary,
            log.Delays,
            log.Issues,
            log.SafetyNotes,
            log.Remarks,
            log.CreatedAtUtc
        );
    }
}
