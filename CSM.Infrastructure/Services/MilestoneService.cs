using CSM.Application.Common.Exceptions;
using CSM.Application.Projects.Milestones;
using CSM.Application.Projects.Milestones.Dtos;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Projects;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public sealed class MilestoneService : IMilestoneService
{
    private readonly ApplicationDbContext _dbContext;

    public MilestoneService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<MilestoneResponse>> GetAllAsync(
        Guid currentUserId,
        Guid projectId,
        Guid? projectPhaseId = null,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        var project = await GetProjectAsync(
            projectId,
            cancellationToken);

        EnsureProjectAccess(actor, project);

        if (projectPhaseId.HasValue)
        {
            await ValidatePhaseAsync(
                project.Id,
                projectPhaseId.Value,
                cancellationToken);
        }

        IQueryable<Milestone> query =
            _dbContext.Milestones
                .AsNoTracking()
                .Where(x => x.ProjectId == project.Id);

        if (projectPhaseId.HasValue)
        {
            query = query.Where(
                x => x.ProjectPhaseId == projectPhaseId.Value);
        }

        var milestones = await query
            .OrderBy(x => x.PlannedDate)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return milestones
            .Select(Map)
            .ToArray();
    }

    public async Task<MilestoneResponse> GetByIdAsync(
        Guid currentUserId,
        Guid milestoneId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        var milestone = await GetMilestoneAsync(
            milestoneId,
            cancellationToken);

        EnsureMilestoneAccess(actor, milestone);

        return Map(milestone);
    }

    public async Task<MilestoneResponse> CreateAsync(
        Guid currentUserId,
        CreateMilestoneRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanModify(actor);
        ValidateName(request.Name);

        var project = await GetProjectAsync(
            request.ProjectId,
            cancellationToken);

        EnsureProjectAccess(actor, project);
        EnsureProjectCanBeModified(project);

        if (request.ProjectPhaseId.HasValue)
        {
            await ValidatePhaseAsync(
                project.Id,
                request.ProjectPhaseId.Value,
                cancellationToken);
        }

        var milestoneNumber =
            await GetNextMilestoneNumberAsync(
                project.CompanyId,
                cancellationToken);

        var milestone = new Milestone
        {
            CompanyId = project.CompanyId,
            MilestoneNumber = milestoneNumber,
            ProjectId = project.Id,
            ProjectPhaseId = request.ProjectPhaseId,
            Name = request.Name.Trim(),
            Description = Clean(request.Description),
            PlannedDate = request.PlannedDate,
            ActualDate = null,
            Status = MilestoneStatus.Pending
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

                _dbContext.Milestones.Add(milestone);

                await _dbContext.SaveChangesAsync(
                    cancellationToken);

                await transaction.CommitAsync(
                    cancellationToken);
            });

        return Map(milestone);
    }

    public async Task<MilestoneResponse> UpdateAsync(
        Guid currentUserId,
        Guid milestoneId,
        UpdateMilestoneRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanModify(actor);
        ValidateName(request.Name);

        var milestone = await GetMilestoneAsync(
            milestoneId,
            cancellationToken);

        EnsureMilestoneAccess(actor, milestone);

        var project = await GetProjectAsync(
            milestone.ProjectId,
            cancellationToken);

        EnsureProjectCanBeModified(project);

        if (milestone.Status == MilestoneStatus.Completed)
        {
            throw new MilestoneManagementException(
                "A completed milestone cannot be edited. Reopen it first.");
        }

        if (request.ProjectPhaseId.HasValue)
        {
            await ValidatePhaseAsync(
                project.Id,
                request.ProjectPhaseId.Value,
                cancellationToken);
        }

        milestone.ProjectPhaseId =
            request.ProjectPhaseId;

        milestone.Name =
            request.Name.Trim();

        milestone.Description =
            Clean(request.Description);

        milestone.PlannedDate =
            request.PlannedDate;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(milestone);
    }

    public async Task<MilestoneResponse> CompleteAsync(
        Guid currentUserId,
        Guid milestoneId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanModify(actor);

        var milestone = await GetMilestoneAsync(
            milestoneId,
            cancellationToken);

        EnsureMilestoneAccess(actor, milestone);

        var project = await GetProjectAsync(
            milestone.ProjectId,
            cancellationToken);

        EnsureProjectCanBeModified(project);

        if (milestone.Status == MilestoneStatus.Completed)
        {
            return Map(milestone);
        }

        milestone.Status = MilestoneStatus.Completed;
        milestone.ActualDate = DateOnly.FromDateTime(DateTime.UtcNow);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(milestone);
    }

    public async Task<MilestoneResponse> ReopenAsync(
        Guid currentUserId,
        Guid milestoneId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanModify(actor);

        var milestone = await GetMilestoneAsync(
            milestoneId,
            cancellationToken);

        EnsureMilestoneAccess(actor, milestone);

        var project = await GetProjectAsync(
            milestone.ProjectId,
            cancellationToken);

        EnsureProjectCanBeModified(project);

        if (milestone.Status != MilestoneStatus.Completed)
        {
            return Map(milestone);
        }

        milestone.Status = MilestoneStatus.Pending;
        milestone.ActualDate = null;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(milestone);
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
            throw new MilestoneManagementException(
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
            ?? throw new MilestoneManagementException(
                "Project was not found.");
    }

    private async Task<Milestone> GetMilestoneAsync(
        Guid milestoneId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Milestones
            .SingleOrDefaultAsync(
                x => x.Id == milestoneId,
                cancellationToken)
            ?? throw new MilestoneManagementException(
                "Milestone was not found.");
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
            throw new MilestoneManagementException(
                "Project phase was not found.");
        }

        if (phase.ProjectId != projectId)
        {
            throw new MilestoneManagementException(
                "The selected project phase does not belong to this project.");
        }
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
            throw new MilestoneManagementException(
                "You cannot access a project outside your company.");
        }
    }

    private static void EnsureMilestoneAccess(
        User actor,
        Milestone milestone)
    {
        if (IsSuperAdmin(actor))
        {
            return;
        }

        EnsureProjectRole(actor);

        if (!actor.CompanyId.HasValue ||
            actor.CompanyId.Value != milestone.CompanyId)
        {
            throw new MilestoneManagementException(
                "You cannot access a milestone outside your company.");
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

        throw new MilestoneManagementException(
            "You are not authorized to create or modify milestones.");
    }

    private static void EnsureProjectRole(User actor)
    {
        if (HasRole(actor, AppRoles.CompanyAdmin) ||
            HasRole(actor, AppRoles.SiteManager) ||
            HasRole(actor, AppRoles.Supervisor))
        {
            return;
        }

        throw new MilestoneManagementException(
            "You are not authorized to access milestones.");
    }

    private static void EnsureProjectCanBeModified(
        Project project)
    {
        if (project.Status is
            ProjectStatus.Completed or
            ProjectStatus.Cancelled or
            ProjectStatus.Closed)
        {
            throw new MilestoneManagementException(
                $"Milestones cannot be modified while the project is {project.Status}.");
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

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new MilestoneManagementException(
                "Milestone name is required.");
        }
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private async Task<string> GetNextMilestoneNumberAsync(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var lastMilestoneNumber =
            await _dbContext.Milestones
                .Where(x => x.CompanyId == companyId)
                .OrderByDescending(x => x.MilestoneNumber)
                .Select(x => x.MilestoneNumber)
                .FirstOrDefaultAsync(
                    cancellationToken);

        var nextNumber = 1;

        if (!string.IsNullOrWhiteSpace(lastMilestoneNumber) &&
            lastMilestoneNumber.StartsWith(
                "MS-",
                StringComparison.OrdinalIgnoreCase))
        {
            var numericPart =
                lastMilestoneNumber.Substring(3);

            if (int.TryParse(
                    numericPart,
                    out var currentNumber))
            {
                nextNumber = currentNumber + 1;
            }
        }

        return $"MS-{nextNumber:D6}";
    }

    private static MilestoneResponse Map(
        Milestone milestone)
    {
        return new MilestoneResponse(
            milestone.Id,
            milestone.MilestoneNumber,
            milestone.CompanyId,
            milestone.ProjectId,
            milestone.ProjectPhaseId,
            milestone.Name,
            milestone.Description,
            milestone.PlannedDate,
            milestone.ActualDate,
            milestone.Status == MilestoneStatus.Completed,
            milestone.CreatedAtUtc);
    }
}