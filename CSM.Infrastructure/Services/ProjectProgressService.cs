using CSM.Application.Projects.Progress;
using CSM.Application.Projects.Progress.Dtos;
using CSM.Domain.Entities.Projects;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using CSM.Domain.Enums;
using CSM.Application.Common.Exceptions;
using CSM.Application.Common.Security;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Constants;


namespace CSM.Infrastructure.Services;

public sealed class ProjectProgressService : IProjectProgressService
{
    private readonly ApplicationDbContext _context;

    public ProjectProgressService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ProjectProgressResponse?> GetByProjectAsync(
        Guid projectId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var project = await _context.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == projectId, cancellationToken);

        if (project is null)
            return null;

        var tasks = await _context.WorkTasks
            .AsNoTracking()
            .Where(x => x.ProjectPhase != null &&
                        x.ProjectPhase.ProjectId == projectId)
            .Include(x => x.ProjectPhase)
            .ToListAsync(cancellationToken);

        var totalTasks = tasks.Count;

        var completed = tasks.Count(x => x.Status == WorkTaskStatus.Completed);
        var inProgress = tasks.Count(x => x.Status == WorkTaskStatus.InProgress);
        var notStarted = tasks.Count(x => x.Status == WorkTaskStatus.NotStarted);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var overdue = tasks.Count(x =>
            x.Status != WorkTaskStatus.Completed &&
            x.PlannedEndDate.HasValue &&
            x.PlannedEndDate.Value < today);

        var overallProgress = totalTasks == 0
            ? 0
            : tasks.Average(x => x.ProgressPercentage);

        decimal estimatedHours = 0m;
        decimal actualHours = 0m;

        string health;

        if (totalTasks == 0)
        {
            health = "NotStarted";
        }
        else if (overdue > 0)
        {
            health = "Delayed";
        }
        else if (overallProgress >= 75)
        {
            health = "OnSchedule";
        }
        else
        {
            health = "AtRisk";
        }

        return new ProjectProgressResponse
        {
            ProjectId = project.Id,
            ProjectName = project.Name,
            TotalTasks = totalTasks,
            CompletedTasks = completed,
            InProgressTasks = inProgress,
            NotStartedTasks = notStarted,
            OverdueTasks = overdue,
            OverallProgress = overallProgress,
            EstimatedHours = estimatedHours,
            ActualHours = actualHours,
            HealthStatus = health
        };
    }

    public async Task<ProjectProgressDashboardResponse> GetDashboardAsync(
        Guid currentUserId,
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var actor = await _context.Users
            .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(
                x => x.Id == currentUserId,
                cancellationToken);

        if (actor is null || !actor.IsActive)
        {
            throw new ProjectManagementException(
                "Current user was not found or is inactive.");
        }

        var project = await _context.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken)
            ?? throw new KeyNotFoundException("Project not found.");

        if (!actor.UserRoles.Any(
                x =>
                    !x.IsDeleted &&
                    !x.Role.IsDeleted &&
                    string.Equals(
                        x.Role.Name,
                        AppRoles.SuperAdmin,
                        StringComparison.OrdinalIgnoreCase)))
        {
            var isCompanyAdmin = actor.UserRoles.Any(
                x =>
                    !x.IsDeleted &&
                    !x.Role.IsDeleted &&
                    string.Equals(
                        x.Role.Name,
                        AppRoles.CompanyAdmin,
                        StringComparison.OrdinalIgnoreCase));

            if (!actor.CompanyId.HasValue ||
                actor.CompanyId.Value != project.CompanyId)
            {
                throw new ProjectManagementException(
                    "You cannot access a project outside your company.");
            }

            if (!isCompanyAdmin)
            {
                var hasSiteAccess = await _context.UserSiteAccesses
                    .AnyAsync(
                        x =>
                            x.UserId == actor.Id &&
                            x.SiteId == project.ConstructionSiteId &&
                            x.CompanyId == project.CompanyId &&
                            x.IsActive,
                        cancellationToken);

                if (!hasSiteAccess)
                {
                    throw new ProjectManagementException(
                        "You are not assigned to this project's site.");
                }
            }
        }

        var totalTasks = await _context.WbsTasks
            .CountAsync(x => x.ProjectId == projectId, cancellationToken);

        var completedTasks = await _context.WbsTasks
            .CountAsync(
                x => x.ProjectId == projectId &&
                     x.Status == CSM.Domain.Enums.WbsTaskStatus.Completed,
                cancellationToken);

        var remainingTasks = totalTasks - completedTasks;

        var plannedProgress = totalTasks == 0
            ? 0m
            : Math.Round((decimal)completedTasks / totalTasks * 100m, 2);

        var actualProgress = plannedProgress;

        var openDelays = await _context.ProjectDelays
            .CountAsync(
                x => x.ProjectId == projectId &&
                     !x.IsResolved &&
                     !x.IsDeleted,
                cancellationToken);

        var totalDelayDays = await _context.ProjectDelays
            .Where(x => x.ProjectId == projectId && !x.IsDeleted)
            .SumAsync(x => (int?)x.DelayDays, cancellationToken) ?? 0;

        return new ProjectProgressDashboardResponse(
            project.Id,
            project.Name,
            plannedProgress,
            actualProgress,
            totalTasks,
            completedTasks,
            remainingTasks,
            openDelays,
            totalDelayDays);
    }
}