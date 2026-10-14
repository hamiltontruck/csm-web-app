using CSM.Application.Common.Exceptions;
using CSM.Application.Projects.Tasks;
using CSM.Application.Projects.Tasks.Dtos;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Projects;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public sealed class WorkTaskService : IWorkTaskService
{
    private readonly ApplicationDbContext _dbContext;

    public WorkTaskService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<WorkTaskResponse>> GetAllAsync(
        Guid currentUserId,
        Guid projectId,
        Guid? projectPhaseId = null,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(currentUserId, cancellationToken);
        var project = await GetProjectAsync(projectId, cancellationToken);

        await EnsureProjectAccess(
            actor,
            project,
            cancellationToken);

        if (projectPhaseId.HasValue)
        {
            await ValidatePhaseAsync(
                project.Id,
                projectPhaseId.Value,
                cancellationToken);
        }

        IQueryable<WorkTask> query = _dbContext.WorkTasks
            .AsNoTracking()
            .Include(x => x.Dependencies)
            .Where(x => x.ProjectId == project.Id);

        if (projectPhaseId.HasValue)
        {
            query = query.Where(
                x => x.ProjectPhaseId == projectPhaseId.Value);
        }

        var tasks = await query
            .OrderBy(x => x.TaskNumber)
            .ToListAsync(cancellationToken);

        return tasks.Select(Map).ToArray();
    }

    public async Task<WorkTaskResponse> GetByIdAsync(
        Guid currentUserId,
        Guid taskId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(currentUserId, cancellationToken);
        var task = await GetTaskAsync(taskId, cancellationToken);

        await EnsureTaskAccess(
            actor,
            task,
            cancellationToken);

        return Map(task);
    }

    public async Task<WorkTaskResponse> CreateAsync(
        Guid currentUserId,
        CreateWorkTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(currentUserId, cancellationToken);

        EnsureCanModify(actor);

        ValidateInput(
            request.TaskNumber,
            request.Title,
            request.PlannedStartDate,
            request.PlannedEndDate,
            request.Priority);

        var project = await GetProjectAsync(
            request.ProjectId,
            cancellationToken);

        await EnsureProjectAccess(
            actor,
            project,
            cancellationToken);
        EnsureProjectCanBeModified(project);

        if (request.ProjectPhaseId.HasValue)
        {
            var phase = await _dbContext.ProjectPhases
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.Id == request.ProjectPhaseId.Value,
                    cancellationToken)
                ?? throw new WorkTaskManagementException(
                    "Project phase was not found.");

            if (phase.ProjectId != project.Id)
            {
                throw new WorkTaskManagementException(
                    "The selected project phase does not belong to this project.");
            }

            if (phase.Status is
                ProjectPhaseStatus.Completed or
                ProjectPhaseStatus.Cancelled)
            {
                throw new WorkTaskManagementException(
                    $"Tasks cannot be added to a {phase.Status} project phase.");
            }
        }

        if (request.MilestoneId.HasValue)
        {
            await ValidateMilestoneAsync(
                project.Id,
                request.ProjectPhaseId,
                request.MilestoneId.Value,
                cancellationToken);
        }

        var normalizedTaskNumber =
            request.TaskNumber.Trim().ToUpperInvariant();

        var numberExists = await _dbContext.WorkTasks
            .IgnoreQueryFilters()
            .AnyAsync(
                x =>
                    x.ProjectId == project.Id &&
                    x.TaskNumber.ToUpper() == normalizedTaskNumber,
                cancellationToken);

        if (numberExists)
        {
            throw new WorkTaskManagementException(
                "A task with this task number already exists in the project.");
        }

        var task = new WorkTask
        {
            CompanyId = project.CompanyId,
            ProjectId = project.Id,
            ProjectPhaseId = request.ProjectPhaseId,
            MilestoneId = request.MilestoneId,
            TaskNumber = normalizedTaskNumber,
            Title = request.Title.Trim(),
            Description = Clean(request.Description),
            PlannedStartDate = request.PlannedStartDate,
            PlannedEndDate = request.PlannedEndDate,
            ActualStartDate = null,
            ActualEndDate = null,
            ProgressPercentage = 0m,
            Priority = request.Priority,
            Status = WorkTaskStatus.NotStarted
        };

        _dbContext.WorkTasks.Add(task);

        await _dbContext.SaveChangesAsync(cancellationToken);

        await RecalculateProgressAsync(
            project.Id,
            task.ProjectPhaseId,
            cancellationToken);

        return Map(task);
    }

    public async Task<WorkTaskResponse> UpdateAsync(
        Guid currentUserId,
        Guid taskId,
        UpdateWorkTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(currentUserId, cancellationToken);

        EnsureCanModify(actor);

        ValidateInput(
            "EXISTING",
            request.Title,
            request.PlannedStartDate,
            request.PlannedEndDate,
            request.Priority);

        var task = await GetTaskAsync(taskId, cancellationToken);

        await EnsureTaskAccess(
            actor,
            task,
            cancellationToken);

        var project = await GetProjectAsync(
            task.ProjectId,
            cancellationToken);

        EnsureProjectCanBeModified(project);

        if (task.Status is
            WorkTaskStatus.Completed or
            WorkTaskStatus.Cancelled)
        {
            throw new WorkTaskManagementException(
                $"A {task.Status} task cannot be edited.");
        }

        if (request.ProjectPhaseId.HasValue)
        {
            await ValidatePhaseAsync(
                project.Id,
                request.ProjectPhaseId.Value,
                cancellationToken);
        }

        if (request.MilestoneId.HasValue)
        {
            await ValidateMilestoneAsync(
                project.Id,
                request.ProjectPhaseId,
                request.MilestoneId.Value,
                cancellationToken);
        }

        var previousPhaseId = task.ProjectPhaseId;

        task.ProjectPhaseId = request.ProjectPhaseId;
        task.MilestoneId = request.MilestoneId;
        task.Title = request.Title.Trim();
        task.Description = Clean(request.Description);
        task.PlannedStartDate = request.PlannedStartDate;
        task.PlannedEndDate = request.PlannedEndDate;
        task.Priority = request.Priority;

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (previousPhaseId != task.ProjectPhaseId)
        {
            await RecalculateProgressAsync(
                project.Id,
                previousPhaseId,
                cancellationToken);
        }

        await RecalculateProgressAsync(
            project.Id,
            task.ProjectPhaseId,
            cancellationToken);

        return Map(task);
    }

    public async Task<WorkTaskResponse> ChangeStatusAsync(
        Guid currentUserId,
        Guid taskId,
        ChangeWorkTaskStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(currentUserId, cancellationToken);

        EnsureCanModify(actor);

        var task = await GetTaskAsync(taskId, cancellationToken);

        await EnsureTaskAccess(
            actor,
            task,
            cancellationToken);

        var project = await GetProjectAsync(
            task.ProjectId,
            cancellationToken);

        EnsureProjectCanBeModified(project);

        if (task.Status == request.Status)
        {
            return Map(task);
        }

        if (!IsValidTransition(task.Status, request.Status))
        {
            throw new WorkTaskManagementException(
                $"Invalid task status transition from {task.Status} to {request.Status}.");
        }

        if (request.Status == WorkTaskStatus.InProgress)
        {
            await EnsureDependenciesCompletedAsync(
                task.Id,
                cancellationToken);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        if (request.Status == WorkTaskStatus.InProgress &&
            !task.ActualStartDate.HasValue)
        {
            task.ActualStartDate = today;
        }

        if (request.Status == WorkTaskStatus.Completed)
        {
            if (!task.ActualStartDate.HasValue)
            {
                task.ActualStartDate = today;
            }

            task.ActualEndDate = today;
            task.ProgressPercentage = 100m;
        }

        task.Status = request.Status;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await RecalculateProgressAsync(
            task.ProjectId,
            task.ProjectPhaseId,
            cancellationToken);

        return Map(task);
    }

    public async Task<WorkTaskResponse> UpdateProgressAsync(
        Guid currentUserId,
        Guid taskId,
        UpdateWorkTaskProgressRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(currentUserId, cancellationToken);

        EnsureCanModify(actor);

        var task = await GetTaskAsync(taskId, cancellationToken);

        await EnsureTaskAccess(
            actor,
            task,
            cancellationToken);

        var project = await GetProjectAsync(
            task.ProjectId,
            cancellationToken);

        EnsureProjectCanBeModified(project);

        if (task.Status is
            WorkTaskStatus.Completed or
            WorkTaskStatus.Cancelled)
        {
            throw new WorkTaskManagementException(
                $"Progress cannot be changed for a {task.Status} task.");
        }

        if (task.Status == WorkTaskStatus.NotStarted &&
            request.ProgressPercentage > 0m)
        {
            throw new WorkTaskManagementException(
                "Start the task before recording progress.");
        }

        if (request.ProgressPercentage < 0m ||
            request.ProgressPercentage >= 100m)
        {
            throw new WorkTaskManagementException(
                "Task progress must be between 0 and less than 100. Complete the task to set progress to 100.");
        }

        task.ProgressPercentage =
            request.ProgressPercentage;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await RecalculateProgressAsync(
            task.ProjectId,
            task.ProjectPhaseId,
            cancellationToken);

        return Map(task);
    }

    public async Task<WorkTaskResponse> AddDependencyAsync(
        Guid currentUserId,
        Guid taskId,
        AddWorkTaskDependencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanModify(actor);

        if (taskId == request.DependsOnTaskId)
        {
            throw new WorkTaskManagementException(
                "A task cannot depend on itself.");
        }

        var task = await GetTaskAsync(
            taskId,
            cancellationToken);

        await EnsureTaskAccess(
            actor,
            task,
            cancellationToken);

        var project = await GetProjectAsync(
            task.ProjectId,
            cancellationToken);

        EnsureProjectCanBeModified(project);

        if (task.Status is
            WorkTaskStatus.Completed or
            WorkTaskStatus.Cancelled)
        {
            throw new WorkTaskManagementException(
                $"Dependencies cannot be changed for a {task.Status} task.");
        }

        var dependsOnTask = await GetTaskAsync(
            request.DependsOnTaskId,
            cancellationToken);

        await EnsureTaskAccess(
            actor,
            dependsOnTask,
            cancellationToken);

        if (dependsOnTask.ProjectId != task.ProjectId)
        {
            throw new WorkTaskManagementException(
                "A task can only depend on another task in the same project.");
        }

        if (dependsOnTask.CompanyId != task.CompanyId)
        {
            throw new WorkTaskManagementException(
                "Dependency tasks must belong to the same company.");
        }

        var exists = await _dbContext.WorkTaskDependencies
            .AnyAsync(
                x =>
                    x.WorkTaskId == task.Id &&
                    x.DependsOnTaskId == dependsOnTask.Id,
                cancellationToken);

        if (exists)
        {
            throw new WorkTaskManagementException(
                "This task dependency already exists.");
        }

        var createsCycle = await WouldCreateCycleAsync(
            task.Id,
            dependsOnTask.Id,
            cancellationToken);

        if (createsCycle)
        {
            throw new WorkTaskManagementException(
                "This dependency would create a circular task dependency.");
        }

        var dependency = new WorkTaskDependency
        {
            CompanyId = task.CompanyId,
            WorkTaskId = task.Id,
            DependsOnTaskId = dependsOnTask.Id
        };

        _dbContext.WorkTaskDependencies.Add(dependency);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return await GetTaskResponseAsync(
            task.Id,
            cancellationToken);
    }

    public async Task<WorkTaskResponse> RemoveDependencyAsync(
        Guid currentUserId,
        Guid taskId,
        Guid dependsOnTaskId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanModify(actor);

        var task = await GetTaskAsync(
            taskId,
            cancellationToken);

        await EnsureTaskAccess(
            actor,
            task,
            cancellationToken);

        var project = await GetProjectAsync(
            task.ProjectId,
            cancellationToken);

        EnsureProjectCanBeModified(project);

        if (task.Status is
            WorkTaskStatus.Completed or
            WorkTaskStatus.Cancelled)
        {
            throw new WorkTaskManagementException(
                $"Dependencies cannot be changed for a {task.Status} task.");
        }

        var dependency = await _dbContext.WorkTaskDependencies
            .SingleOrDefaultAsync(
                x =>
                    x.WorkTaskId == task.Id &&
                    x.DependsOnTaskId == dependsOnTaskId,
                cancellationToken);

        if (dependency is null)
        {
            throw new WorkTaskManagementException(
                "Task dependency was not found.");
        }

        _dbContext.WorkTaskDependencies.Remove(dependency);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return await GetTaskResponseAsync(
            task.Id,
            cancellationToken);
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
            throw new WorkTaskManagementException(
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
            ?? throw new WorkTaskManagementException(
                "Project was not found.");
    }

    private async Task<WorkTask> GetTaskAsync(
        Guid taskId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.WorkTasks
            .Include(x => x.Dependencies)
            .SingleOrDefaultAsync(
                x => x.Id == taskId,
                cancellationToken)
            ?? throw new WorkTaskManagementException(
                "Work task was not found.");
    }

    private async Task ValidatePhaseAsync(
        Guid projectId,
        Guid phaseId,
        CancellationToken cancellationToken)
    {
        var phase = await _dbContext.ProjectPhases
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == phaseId,
                cancellationToken);

        if (phase is null)
        {
            throw new WorkTaskManagementException(
                "Project phase was not found.");
        }

        if (phase.ProjectId != projectId)
        {
            throw new WorkTaskManagementException(
                "The selected project phase does not belong to this project.");
        }
    }

    private async Task ValidateMilestoneAsync(
        Guid projectId,
        Guid? phaseId,
        Guid milestoneId,
        CancellationToken cancellationToken)
    {
        var milestone = await _dbContext.Milestones
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == milestoneId,
                cancellationToken);

        if (milestone is null)
        {
            throw new WorkTaskManagementException(
                "Milestone was not found.");
        }

        if (milestone.ProjectId != projectId)
        {
            throw new WorkTaskManagementException(
                "The selected milestone does not belong to this project.");
        }

        if (milestone.ProjectPhaseId.HasValue &&
            milestone.ProjectPhaseId != phaseId)
        {
            throw new WorkTaskManagementException(
                "The selected milestone does not belong to the selected project phase.");
        }
    }

    private async Task EnsureDependenciesCompletedAsync(
        Guid taskId,
        CancellationToken cancellationToken)
    {
        var dependencies = await _dbContext.WorkTaskDependencies
            .AsNoTracking()
            .Where(x => x.WorkTaskId == taskId)
            .Join(
                _dbContext.WorkTasks.AsNoTracking(),
                dependency => dependency.DependsOnTaskId,
                task => task.Id,
                (dependency, task) => new
                {
                    task.TaskNumber,
                    task.Title,
                    task.Status
                })
            .ToListAsync(cancellationToken);

        var incomplete = dependencies
            .Where(x => x.Status != WorkTaskStatus.Completed)
            .ToArray();

        if (incomplete.Length == 0)
        {
            return;
        }

        var taskNumbers = string.Join(
            ", ",
            incomplete.Select(x => x.TaskNumber));

        throw new WorkTaskManagementException(
            $"This task cannot start until its dependencies are completed: {taskNumbers}.");
    }

    private async Task RecalculateProgressAsync(
        Guid projectId,
        Guid? phaseId,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // ---------------------------------------------------------
        // 1. Synchronize milestones from their non-cancelled tasks
        // ---------------------------------------------------------

        var milestoneIds = await _dbContext.WorkTasks
            .Where(x =>
                x.ProjectId == projectId &&
                x.MilestoneId.HasValue &&
                x.Status != WorkTaskStatus.Cancelled)
            .Select(x => x.MilestoneId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (milestoneIds.Count > 0)
        {
            var milestones = await _dbContext.Milestones
                .Where(x => milestoneIds.Contains(x.Id))
                .ToListAsync(cancellationToken);

            foreach (var milestone in milestones)
            {
                var taskStatuses = await _dbContext.WorkTasks
                    .Where(x =>
                        x.MilestoneId == milestone.Id &&
                        x.Status != WorkTaskStatus.Cancelled)
                    .Select(x => x.Status)
                    .ToListAsync(cancellationToken);

                // Do not auto-complete an empty milestone.
                if (taskStatuses.Count == 0)
                {
                    continue;
                }

                var allCompleted = taskStatuses.All(
                    x => x == WorkTaskStatus.Completed);

                if (allCompleted)
                {
                    milestone.Status = MilestoneStatus.Completed;

                    if (!milestone.ActualDate.HasValue)
                    {
                        milestone.ActualDate = today;
                    }

                    milestone.ProgressPercentage = 100m;
                }
                else
                {
                    milestone.Status = MilestoneStatus.InProgress;
                    milestone.ActualDate = null;

                    var completedCount = taskStatuses.Count(
                        x => x == WorkTaskStatus.Completed);

                    milestone.ProgressPercentage =
                        taskStatuses.Count == 0
                            ? 0m
                            : completedCount * 100m / taskStatuses.Count;
                }
            }
        }

        // ---------------------------------------------------------
        // 2. Recalculate the affected phase
        // ---------------------------------------------------------

        if (phaseId.HasValue)
        {
            var phase = await _dbContext.ProjectPhases
                .SingleOrDefaultAsync(
                    x => x.Id == phaseId.Value,
                    cancellationToken);

            if (phase is not null)
            {
                var phaseTasks = await _dbContext.WorkTasks
                    .Where(x =>
                        x.ProjectPhaseId == phase.Id &&
                        x.Status != WorkTaskStatus.Cancelled)
                    .Select(x => new
                    {
                        x.ProgressPercentage,
                        x.Status
                    })
                    .ToListAsync(cancellationToken);

                if (phaseTasks.Count == 0)
                {
                    phase.ProgressPercentage = 0m;
                }
                else
                {
                    phase.ProgressPercentage =
                        phaseTasks.Average(
                            x => x.ProgressPercentage);

                    var allCompleted = phaseTasks.All(
                        x => x.Status == WorkTaskStatus.Completed);

                    if (allCompleted)
                    {
                        phase.ProgressPercentage = 100m;
                        phase.Status = ProjectPhaseStatus.Completed;

                        if (!phase.ActualStartDate.HasValue)
                        {
                            phase.ActualStartDate = today;
                        }

                        if (!phase.ActualEndDate.HasValue)
                        {
                            phase.ActualEndDate = today;
                        }
                    }
                }
            }
        }

        // ---------------------------------------------------------
        // 3. Recalculate overall project progress
        // ---------------------------------------------------------

        var project = await _dbContext.Projects
            .SingleOrDefaultAsync(
                x => x.Id == projectId,
                cancellationToken);

        if (project is not null)
        {
            var projectTaskProgress =
                await _dbContext.WorkTasks
                    .Where(x =>
                        x.ProjectId == project.Id &&
                        x.Status != WorkTaskStatus.Cancelled)
                    .Select(x => (decimal?)x.ProgressPercentage)
                    .AverageAsync(cancellationToken);

            project.ProgressPercentage =
                projectTaskProgress ?? 0m;
        }

        await _dbContext.SaveChangesAsync(
            cancellationToken);
    }

    private async Task<bool> WouldCreateCycleAsync(
        Guid taskId,
        Guid dependsOnTaskId,
        CancellationToken cancellationToken)
    {
        var dependencies = await _dbContext.WorkTaskDependencies
            .AsNoTracking()
            .Select(x => new
            {
                x.WorkTaskId,
                x.DependsOnTaskId
            })
            .ToListAsync(cancellationToken);

        var dependencyMap = dependencies
            .GroupBy(x => x.WorkTaskId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(x => x.DependsOnTaskId)
                    .ToArray());

        var visited = new HashSet<Guid>();
        var stack = new Stack<Guid>();

        stack.Push(dependsOnTaskId);

        while (stack.Count > 0)
        {
            var current = stack.Pop();

            if (current == taskId)
            {
                return true;
            }

            if (!visited.Add(current))
            {
                continue;
            }

            if (!dependencyMap.TryGetValue(
                    current,
                    out var nextTasks))
            {
                continue;
            }

            foreach (var nextTaskId in nextTasks)
            {
                stack.Push(nextTaskId);
            }
        }

        return false;
    }

    private async Task<WorkTaskResponse> GetTaskResponseAsync(
        Guid taskId,
        CancellationToken cancellationToken)
    {
        var task = await _dbContext.WorkTasks
            .AsNoTracking()
            .Include(x => x.Dependencies)
            .SingleOrDefaultAsync(
                x => x.Id == taskId,
                cancellationToken)
            ?? throw new WorkTaskManagementException(
                "Work task was not found.");

        return Map(task);
    }

    private async Task EnsureProjectAccess(
        User actor,
        Project project,
        CancellationToken cancellationToken)
    {
        if (IsSuperAdmin(actor))
        {
            return;
        }

        EnsureProjectRole(actor);

        if (!actor.CompanyId.HasValue ||
            actor.CompanyId.Value != project.CompanyId)
        {
            throw new WorkTaskManagementException(
                "You cannot access a project outside your company.");
        }

        if (HasRole(actor, AppRoles.CompanyAdmin))
        {
            return;
        }

        var hasSiteAccess = await _dbContext.UserSiteAccesses
            .AnyAsync(
                access =>
                    access.UserId == actor.Id &&
                    access.SiteId == project.ConstructionSiteId &&
                    access.CompanyId == project.CompanyId &&
                    access.IsActive,
                cancellationToken);

        if (!hasSiteAccess)
        {
            throw new WorkTaskManagementException(
                "You are not assigned to this project's site.");
        }
    }

    private async Task EnsureTaskAccess(
        User actor,
        WorkTask task,
        CancellationToken cancellationToken)
    {
        if (IsSuperAdmin(actor))
        {
            return;
        }

        EnsureProjectRole(actor);

        if (!actor.CompanyId.HasValue ||
            actor.CompanyId.Value != task.CompanyId)
        {
            throw new WorkTaskManagementException(
                "You cannot access a task outside your company.");
        }

        if (HasRole(actor, AppRoles.CompanyAdmin))
        {
            return;
        }

        var projectSiteId = await _dbContext.Projects
            .Where(
                project =>
                    project.Id == task.ProjectId &&
                    project.CompanyId == task.CompanyId &&
                    !project.IsDeleted)
            .Select(project => (Guid?)project.ConstructionSiteId)
            .SingleOrDefaultAsync(cancellationToken);

        if (!projectSiteId.HasValue)
        {
            throw new WorkTaskManagementException(
                "The task's project was not found.");
        }

        var hasSiteAccess = await _dbContext.UserSiteAccesses
            .AnyAsync(
                access =>
                    access.UserId == actor.Id &&
                    access.SiteId == projectSiteId.Value &&
                    access.CompanyId == task.CompanyId &&
                    access.IsActive,
                cancellationToken);

        if (!hasSiteAccess)
        {
            throw new WorkTaskManagementException(
                "You are not assigned to this task's project site.");
        }
    }
    private static void EnsureCanModify(User actor)
    {
        if (IsSuperAdmin(actor))
        {
            return;
        }

        if (HasRole(actor, AppRoles.CompanyAdmin) ||
            HasRole(actor, AppRoles.SiteManager))
        {
            return;
        }

        throw new WorkTaskManagementException(
            "You are not authorized to create or modify work tasks.");
    }

    private static void EnsureProjectRole(User actor)
    {
        if (HasRole(actor, AppRoles.CompanyAdmin) ||
            HasRole(actor, AppRoles.SiteManager) ||
            HasRole(actor, AppRoles.Supervisor))
        {
            return;
        }

        throw new WorkTaskManagementException(
            "You are not authorized to access work tasks.");
    }

    private static void EnsureProjectCanBeModified(
        Project project)
    {
        if (project.Status is
            ProjectStatus.Completed or
            ProjectStatus.Cancelled or
            ProjectStatus.Closed)
        {
            throw new WorkTaskManagementException(
                $"Work tasks cannot be modified while the project is {project.Status}.");
        }
    }

    private static bool IsValidTransition(
        WorkTaskStatus current,
        WorkTaskStatus next)
    {
        return current switch
        {
            WorkTaskStatus.NotStarted =>
                next is WorkTaskStatus.InProgress
                    or WorkTaskStatus.Cancelled,

            WorkTaskStatus.InProgress =>
                next is WorkTaskStatus.Blocked
                    or WorkTaskStatus.OnHold
                    or WorkTaskStatus.Completed
                    or WorkTaskStatus.Cancelled,

            WorkTaskStatus.Blocked =>
                next is WorkTaskStatus.InProgress
                    or WorkTaskStatus.OnHold
                    or WorkTaskStatus.Cancelled,

            WorkTaskStatus.OnHold =>
                next is WorkTaskStatus.InProgress
                    or WorkTaskStatus.Cancelled,

            WorkTaskStatus.Completed => false,
            WorkTaskStatus.Cancelled => false,

            _ => false
        };
    }

    private static void ValidateInput(
        string taskNumber,
        string title,
        DateOnly? plannedStartDate,
        DateOnly? plannedEndDate,
        Priority priority)
    {
        if (string.IsNullOrWhiteSpace(taskNumber))
        {
            throw new WorkTaskManagementException(
                "Task number is required.");
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new WorkTaskManagementException(
                "Task title is required.");
        }

        if (plannedStartDate.HasValue &&
            plannedEndDate.HasValue &&
            plannedEndDate.Value < plannedStartDate.Value)
        {
            throw new WorkTaskManagementException(
                "Planned end date cannot be earlier than planned start date.");
        }

        if (!Enum.IsDefined(priority))
        {
            throw new WorkTaskManagementException(
                "Invalid task priority.");
        }
    }

    private static bool IsSuperAdmin(User user)
    {
        return HasRole(user, AppRoles.SuperAdmin);
    }

    private static bool HasRole(User user, string roleName)
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

    private static WorkTaskResponse Map(WorkTask task)
    {
        return new WorkTaskResponse(
            task.Id,
            task.CompanyId,
            task.ProjectId,
            task.ProjectPhaseId,
            task.MilestoneId,
            task.TaskNumber,
            task.Title,
            task.Description,
            task.PlannedStartDate,
            task.PlannedEndDate,
            task.ActualStartDate,
            task.ActualEndDate,
            task.ProgressPercentage,
            task.Priority,
            task.Status,
            task.Dependencies
                .Select(x => new WorkTaskDependencyResponse(
                    x.Id,
                    x.WorkTaskId,
                    x.DependsOnTaskId))
                .ToArray(),
            task.CreatedAtUtc);
    }
}