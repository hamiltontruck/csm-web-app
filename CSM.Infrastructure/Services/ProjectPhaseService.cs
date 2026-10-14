using CSM.Application.Common.Exceptions;
using CSM.Application.Projects.Phases;
using CSM.Application.Projects.Phases.Dtos;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Projects;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public sealed class ProjectPhaseService : IProjectPhaseService
{
    private readonly ApplicationDbContext _dbContext;

    public ProjectPhaseService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<ProjectPhaseResponse>> GetAllAsync(
        Guid currentUserId,
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        var project = await GetProjectAsync(
            projectId,
            cancellationToken);

        EnsureProjectAccess(actor, project);

        var phases = await _dbContext.ProjectPhases
            .AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .OrderBy(x => x.Sequence)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return phases
            .Select(Map)
            .ToArray();
    }

    public async Task<ProjectPhaseResponse> GetByIdAsync(
        Guid currentUserId,
        Guid phaseId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        var phase = await GetPhaseAsync(
            phaseId,
            cancellationToken);

        EnsurePhaseAccess(actor, phase);

        return Map(phase);
    }

    public async Task<ProjectPhaseResponse> CreateAsync(
        Guid currentUserId,
        CreateProjectPhaseRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanModify(actor);

        ValidateInput(
            request.Name,
            request.Sequence,
            request.PlannedStartDate,
            request.PlannedEndDate);

        var project = await GetProjectAsync(
            request.ProjectId,
            cancellationToken);

        EnsureProjectAccess(actor, project);

        EnsureProjectCanBeModified(project);

        var sequenceExists = await _dbContext.ProjectPhases
            .IgnoreQueryFilters()
            .AnyAsync(
                x =>
                    x.ProjectId == project.Id &&
                    x.Sequence == request.Sequence,
                cancellationToken);

        if (sequenceExists)
        {
            throw new ProjectPhaseManagementException(
                "A phase with this sequence already exists in the project.");
        }
if (await _dbContext.ProjectPhases.AnyAsync(x =>
        x.CompanyId == project.CompanyId &&
        x.ProjectId == project.Id &&
        x.PhaseNumber == request.PhaseNumber,
        cancellationToken))
{
    throw new ProjectPhaseManagementException(
        "A phase with this phase number already exists.");
}

var phase = new ProjectPhase
{
    CompanyId = project.CompanyId,
    PhaseNumber = request.PhaseNumber,
    ProjectId = project.Id,
    Name = request.Name.Trim(),
    Description = Clean(request.Description),
    Sequence = request.Sequence,
    PlannedStartDate = request.PlannedStartDate,
    PlannedEndDate = request.PlannedEndDate,
    ProgressPercentage = 0m,
    Status = ProjectPhaseStatus.NotStarted
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

                _dbContext.ProjectPhases.Add(phase);

                await _dbContext.SaveChangesAsync(
                    cancellationToken);

                await transaction.CommitAsync(
                    cancellationToken);
            });

        return Map(phase);
    }

    public async Task<ProjectPhaseResponse> UpdateAsync(
        Guid currentUserId,
        Guid phaseId,
        UpdateProjectPhaseRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanModify(actor);

        ValidateInput(
            request.Name,
            request.Sequence,
            request.PlannedStartDate,
            request.PlannedEndDate);

        var phase = await GetPhaseAsync(
            phaseId,
            cancellationToken);

        EnsurePhaseAccess(actor, phase);

        var project = await GetProjectAsync(
            phase.ProjectId,
            cancellationToken);

        EnsureProjectCanBeModified(project);

        if (phase.Status is
            ProjectPhaseStatus.Completed or
            ProjectPhaseStatus.Cancelled)
        {
            throw new ProjectPhaseManagementException(
                $"A {phase.Status} phase cannot be edited.");
        }

        var sequenceExists = await _dbContext.ProjectPhases
            .IgnoreQueryFilters()
            .AnyAsync(
                x =>
                    x.ProjectId == phase.ProjectId &&
                    x.Sequence == request.Sequence &&
                    x.Id != phase.Id,
                cancellationToken);

        if (sequenceExists)
        {
            throw new ProjectPhaseManagementException(
                "A phase with this sequence already exists in the project.");
        }

        phase.Name = request.Name.Trim();
        phase.Description = Clean(request.Description);
        phase.Sequence = request.Sequence;
        phase.PlannedStartDate = request.PlannedStartDate;
        phase.PlannedEndDate = request.PlannedEndDate;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(phase);
    }

    public async Task<ProjectPhaseResponse> ChangeStatusAsync(
        Guid currentUserId,
        Guid phaseId,
        ChangeProjectPhaseStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanModify(actor);

        var phase = await GetPhaseAsync(
            phaseId,
            cancellationToken);

        EnsurePhaseAccess(actor, phase);

        var project = await GetProjectAsync(
            phase.ProjectId,
            cancellationToken);

        EnsureProjectCanBeModified(project);

        if (phase.Status == request.Status)
        {
            return Map(phase);
        }

        if (!IsValidTransition(
                phase.Status,
                request.Status))
        {
            throw new ProjectPhaseManagementException(
                $"Invalid project phase status transition from {phase.Status} to {request.Status}.");
        }

        var today = DateOnly.FromDateTime(
            DateTime.UtcNow);

        if (request.Status == ProjectPhaseStatus.InProgress &&
            !phase.ActualStartDate.HasValue)
        {
            phase.ActualStartDate = today;
        }

        if (request.Status == ProjectPhaseStatus.Completed)
        {
            if (!phase.ActualEndDate.HasValue)
            {
                phase.ActualEndDate = today;
            }

            phase.ProgressPercentage = 100m;
        }

        phase.Status = request.Status;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(phase);
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
            throw new ProjectPhaseManagementException(
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
            ?? throw new ProjectPhaseManagementException(
                "Project was not found.");
    }

    private async Task<ProjectPhase> GetPhaseAsync(
        Guid phaseId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.ProjectPhases
            .SingleOrDefaultAsync(
                x => x.Id == phaseId,
                cancellationToken)
            ?? throw new ProjectPhaseManagementException(
                "Project phase was not found.");
    }

    private static void EnsureProjectAccess(
        User actor,
        Project project)
    {
        if (IsSuperAdmin(actor))
        {
            return;
        }

        EnsureProjectRole(actor);

        if (!actor.CompanyId.HasValue ||
            actor.CompanyId.Value != project.CompanyId)
        {
            throw new ProjectPhaseManagementException(
                "You cannot access a project outside your company.");
        }
    }

    private static void EnsurePhaseAccess(
        User actor,
        ProjectPhase phase)
    {
        if (IsSuperAdmin(actor))
        {
            return;
        }

        EnsureProjectRole(actor);

        if (!actor.CompanyId.HasValue ||
            actor.CompanyId.Value != phase.CompanyId)
        {
            throw new ProjectPhaseManagementException(
                "You cannot access a project phase outside your company.");
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

        throw new ProjectPhaseManagementException(
            "You are not authorized to create or modify project phases.");
    }

    private static void EnsureProjectRole(User actor)
    {
        if (HasRole(actor, AppRoles.CompanyAdmin) ||
            HasRole(actor, AppRoles.SiteManager) ||
            HasRole(actor, AppRoles.Supervisor))
        {
            return;
        }

        throw new ProjectPhaseManagementException(
            "You are not authorized to access project phases.");
    }

    private static void EnsureProjectCanBeModified(
        Project project)
    {
        if (project.Status is
            ProjectStatus.Completed or
            ProjectStatus.Cancelled or
            ProjectStatus.Closed)
        {
            throw new ProjectPhaseManagementException(
                $"Project phases cannot be modified while the project is {project.Status}.");
        }
    }

    private static bool IsSuperAdmin(User user)
    {
        return HasRole(user, AppRoles.SuperAdmin);
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

    private static void ValidateInput(
        string name,
        int sequence,
        DateOnly? plannedStartDate,
        DateOnly? plannedEndDate)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ProjectPhaseManagementException(
                "Project phase name is required.");
        }

        if (sequence <= 0)
        {
            throw new ProjectPhaseManagementException(
                "Project phase sequence must be greater than zero.");
        }

        if (plannedStartDate.HasValue &&
            plannedEndDate.HasValue &&
            plannedEndDate.Value <
            plannedStartDate.Value)
        {
            throw new ProjectPhaseManagementException(
                "Planned end date cannot be earlier than planned start date.");
        }
    }

    private static bool IsValidTransition(
        ProjectPhaseStatus current,
        ProjectPhaseStatus next)
    {
        return current switch
        {
            ProjectPhaseStatus.NotStarted =>
                next is ProjectPhaseStatus.InProgress
                    or ProjectPhaseStatus.Cancelled,

            ProjectPhaseStatus.InProgress =>
                next is ProjectPhaseStatus.OnHold
                    or ProjectPhaseStatus.Completed
                    or ProjectPhaseStatus.Cancelled,

            ProjectPhaseStatus.OnHold =>
                next is ProjectPhaseStatus.InProgress
                    or ProjectPhaseStatus.Completed
                    or ProjectPhaseStatus.Cancelled,

            ProjectPhaseStatus.Completed => false,

            ProjectPhaseStatus.Cancelled => false,

            _ => false
        };
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private async Task<string> GetNextPhaseNumberAsync(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var lastPhaseNumber =
            await _dbContext.ProjectPhases
                .Where(x => x.CompanyId == companyId)
                .OrderByDescending(x => x.PhaseNumber)
                .Select(x => x.PhaseNumber)
                .FirstOrDefaultAsync(
                    cancellationToken);

        var nextNumber = 1;

        if (!string.IsNullOrWhiteSpace(lastPhaseNumber) &&
            lastPhaseNumber.StartsWith(
                "PH-",
                StringComparison.OrdinalIgnoreCase))
        {
            var numericPart =
                lastPhaseNumber.Substring(3);

            if (int.TryParse(
                    numericPart,
                    out var currentNumber))
            {
                nextNumber = currentNumber + 1;
            }
        }

        return $"PH-{nextNumber:D6}";
    }

    private static ProjectPhaseResponse Map(
        ProjectPhase phase)
    {
        return new ProjectPhaseResponse(
            phase.Id,
            phase.PhaseNumber,
            phase.CompanyId,
            phase.ProjectId,
            phase.Name,
            phase.Description,
            phase.Sequence,
            phase.PlannedStartDate,
            phase.PlannedEndDate,
            phase.ActualStartDate,
            phase.ActualEndDate,
            phase.ProgressPercentage,
            phase.Status,
            phase.CreatedAtUtc);
    }
}