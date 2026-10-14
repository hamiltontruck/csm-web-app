using CSM.Application.Common.Exceptions;
using CSM.Application.Projects;
using CSM.Application.Projects.Dtos;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Projects;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public sealed class ProjectService : IProjectService
{
    private readonly ApplicationDbContext _dbContext;

    public ProjectService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<ProjectResponse>> GetAllAsync(
        Guid currentUserId,
        Guid? constructionSiteId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(currentUserId, cancellationToken);



        IQueryable<Project> query = _dbContext.Projects
            .AsNoTracking()
            .Include(x => x.ConstructionSite);

        if (!IsSuperAdmin(actor))
        {
            query = query.Where(x => x.CompanyId == actor.CompanyId);

            if (HasRole(actor, AppRoles.CompanyAdmin))
            {
                // CompanyAdmin can see all projects within the company.
            }
            else
            {
                query = query.Where(x =>
                    _dbContext.UserSiteAccesses.Any(
                        access =>
                            access.UserId == actor.Id &&
                            access.SiteId == x.ConstructionSiteId &&
                            access.CompanyId == x.CompanyId &&
                            access.IsActive));
            }
        }

        // Filter by selected construction site
        if (constructionSiteId.HasValue)
        {
            query = query.Where(x =>
                x.ConstructionSiteId == constructionSiteId.Value);
        }

        var projects = await query
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return projects
            .Select(Map)
            .ToArray();
    }

    public async Task<ProjectResponse> GetByIdAsync(
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

        return Map(project);
    }

    public async Task<ProjectResponse> CreateAsync(
        Guid currentUserId,
        CreateProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanModify(actor);

        ValidateInput(
            request.ProjectCode,
            request.Name,
            request.PlannedStartDate,
            request.PlannedEndDate,
            request.ContractValue);

        var site = await _dbContext.ConstructionSites
            .SingleOrDefaultAsync(
                x => x.Id == request.ConstructionSiteId,
                cancellationToken);

        if (site is null)
        {
            throw new ProjectManagementException(
                "Construction site was not found.");
        }

        if (!site.IsActive)
        {
            throw new ProjectManagementException(
                "Cannot create a project for an inactive construction site.");
        }

        if (!IsSuperAdmin(actor))
        {
            if (!actor.CompanyId.HasValue ||
                actor.CompanyId.Value != site.CompanyId)
            {
                throw new ProjectManagementException(
                    "You cannot create a project for a site outside your company.");
            }

            if (!HasRole(actor, AppRoles.CompanyAdmin))
            {
                var hasSiteAccess = await _dbContext.UserSiteAccesses
                    .AnyAsync(
                        access =>
                            access.UserId == actor.Id &&
                            access.SiteId == site.Id &&
                            access.CompanyId == site.CompanyId &&
                            access.IsActive,
                        cancellationToken);

                if (!hasSiteAccess)
                {
                    throw new ProjectManagementException(
                        "You are not assigned to this project's site.");
                }
            }
        }

        var normalizedCode =
            request.ProjectCode.Trim().ToUpperInvariant();

        var codeExists = await _dbContext.Projects
            .IgnoreQueryFilters()
            .AnyAsync(
                x =>
                    x.CompanyId == site.CompanyId &&
                    x.ProjectCode.ToUpper() == normalizedCode,
                cancellationToken);

        if (codeExists)
        {
            throw new ProjectManagementException(
                "A project with this code already exists in the company.");
        }

        var project = new Project
        {
            CompanyId = site.CompanyId,
            ConstructionSiteId = site.Id,
            ProjectCode = normalizedCode,
            Name = request.Name.Trim(),
            Description = Clean(request.Description),
            PlannedStartDate = request.PlannedStartDate,
            PlannedEndDate = request.PlannedEndDate,
            ContractValue = request.ContractValue,
            ProgressPercentage = 0m,
            Status = ProjectStatus.Draft
        };

        _dbContext.Projects.Add(project);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(project);
    }

    public async Task<ProjectResponse> UpdateAsync(
        Guid currentUserId,
        Guid projectId,
        UpdateProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanModify(actor);

        var project = await GetProjectAsync(
            projectId,
            cancellationToken);

        EnsureProjectAccess(actor, project);

        ValidateInput(
            project.ProjectCode,
            request.Name,
            request.PlannedStartDate,
            request.PlannedEndDate,
            request.ContractValue);

        if (project.Status is
            ProjectStatus.Completed or
            ProjectStatus.Cancelled or
            ProjectStatus.Closed)
        {
            throw new ProjectManagementException(
                $"A {project.Status} project cannot be edited.");
        }

        project.Name = request.Name.Trim();
        project.Description = Clean(request.Description);

        project.Status = Enum.Parse<ProjectStatus>(request.Status);

        project.ProgressPercentage = Math.Clamp(
            request.ProgressPercentage,
            0m,
            100m);

        project.PlannedStartDate = request.PlannedStartDate;
        project.PlannedEndDate = request.PlannedEndDate;
        project.ContractValue = request.ContractValue;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(project);
    }

    public async Task<ProjectResponse> ChangeStatusAsync(
        Guid currentUserId,
        Guid projectId,
        ChangeProjectStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanModify(actor);

        var project = await GetProjectAsync(
            projectId,
            cancellationToken);

        EnsureProjectAccess(actor, project);

        if (project.Status == request.Status)
        {
            return Map(project);
        }

        if (!IsValidTransition(
                project.Status,
                request.Status))
        {
            throw new ProjectManagementException(
                $"Invalid project status transition from {project.Status} to {request.Status}.");
        }

        if (request.Status == ProjectStatus.Completed)
        {
            await ValidateProjectCompletionAsync(
                project,
                cancellationToken);
        }
        var today = DateOnly.FromDateTime(
            DateTime.UtcNow);

        if (request.Status == ProjectStatus.Active &&
            !project.ActualStartDate.HasValue)
        {
            project.ActualStartDate = today;
        }

        if (request.Status == ProjectStatus.Completed &&
            !project.ActualEndDate.HasValue)
        {
            project.ActualEndDate = today;

            project.ProgressPercentage = 100m;
        }

        project.Status = request.Status;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(project);
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
            throw new ProjectManagementException(
                "Current user was not found or is inactive.");
        }

        return user;
    }

    private async Task<Project> GetProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Projects
            .Include(p => p.ConstructionSite)
                .ThenInclude(s => s.Company)
            .SingleOrDefaultAsync(
                x => x.Id == projectId,
                cancellationToken)
            ?? throw new ProjectManagementException(
                "Project was not found.");
    }

    private void EnsureProjectAccess(
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
            throw new ProjectManagementException(
                "You cannot access a project outside your company.");
        }

        if (HasRole(actor, AppRoles.CompanyAdmin))
        {
            return;
        }

        var hasSiteAccess = _dbContext.UserSiteAccesses.Any(
            access =>
                access.UserId == actor.Id &&
                access.SiteId == project.ConstructionSiteId &&
                access.CompanyId == project.CompanyId &&
                access.IsActive);

        if (!hasSiteAccess)
        {
            throw new ProjectManagementException(
                "You are not assigned to this project's site.");
        }
    }

    private async Task ValidateProjectCompletionAsync(
        Project project,
        CancellationToken cancellationToken)
    {
        var phases = await _dbContext.ProjectPhases
            .AsNoTracking()
            .Where(x => x.ProjectId == project.Id)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Status,
                x.ProgressPercentage
            })
            .ToListAsync(cancellationToken);

        if (phases.Count == 0)
        {
            throw new ProjectManagementException(
                "The project cannot be completed because it has no project phases.");
        }

        var incompletePhases = phases
            .Where(x => x.Status != ProjectPhaseStatus.Completed)
            .ToArray();

        if (incompletePhases.Length > 0)
        {
            var names = string.Join(
                ", ",
                incompletePhases.Select(x => x.Name));

            throw new ProjectManagementException(
                $"The project cannot be completed until all project phases are completed. Incomplete phases: {names}.");
        }

        var incompleteTasks = await _dbContext.WorkTasks
            .AsNoTracking()
            .Where(x =>
                x.ProjectId == project.Id &&
                x.Status != WorkTaskStatus.Completed &&
                x.Status != WorkTaskStatus.Cancelled)
            .Select(x => x.TaskNumber)
            .ToListAsync(cancellationToken);

        if (incompleteTasks.Count > 0)
        {
            var taskNumbers = string.Join(
                ", ",
                incompleteTasks);

            throw new ProjectManagementException(
                $"The project cannot be completed while work tasks remain incomplete: {taskNumbers}.");
        }

       var incompleteMilestones = await _dbContext.Milestones
           .AsNoTracking()
           .Where(x =>
               x.ProjectId == project.Id &&
               x.Status != MilestoneStatus.Completed)
           .Select(x => x.Name)
           .ToListAsync(cancellationToken);

        if (incompleteMilestones.Count > 0)
        {
            var names = string.Join(
                ", ",
                incompleteMilestones);

            throw new ProjectManagementException(
                $"The project cannot be completed until all milestones are completed: {names}.");
        }

        if (project.ProgressPercentage < 100m)
        {
            throw new ProjectManagementException(
                $"The project cannot be completed because progress is {project.ProgressPercentage:0.##}%.");
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

        throw new ProjectManagementException(
            "You are not authorized to create or modify projects.");
    }

    private static void EnsureProjectRole(User actor)
    {
        if (HasRole(actor, AppRoles.CompanyAdmin) ||
            HasRole(actor, AppRoles.SiteManager) ||
            HasRole(actor, AppRoles.Supervisor))
        {
            return;
        }

        throw new ProjectManagementException(
            "You are not authorized to access projects.");
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
        string projectCode,
        string name,
        DateOnly? plannedStartDate,
        DateOnly? plannedEndDate,
        decimal contractValue)
    {
        if (string.IsNullOrWhiteSpace(projectCode))
        {
            throw new ProjectManagementException(
                "Project code is required.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ProjectManagementException(
                "Project name is required.");
        }

        if (contractValue < 0)
        {
            throw new ProjectManagementException(
                "Contract value cannot be negative.");
        }

        if (plannedStartDate.HasValue &&
            plannedEndDate.HasValue &&
            plannedEndDate.Value <
            plannedStartDate.Value)
        {
            throw new ProjectManagementException(
                "Planned end date cannot be earlier than planned start date.");
        }
    }

    private static bool IsValidTransition(
        ProjectStatus current,
        ProjectStatus next)
    {
        return current switch
        {
            ProjectStatus.Draft =>
                next is ProjectStatus.Planning
                    or ProjectStatus.Cancelled,

            ProjectStatus.Planning =>
                next is ProjectStatus.Active
                    or ProjectStatus.Cancelled,

            ProjectStatus.Active =>
                next is ProjectStatus.OnHold
                    or ProjectStatus.Completed
                    or ProjectStatus.Cancelled,

            ProjectStatus.OnHold =>
                next is ProjectStatus.Active
                    or ProjectStatus.Completed
                    or ProjectStatus.Cancelled,

            ProjectStatus.Completed =>
                next == ProjectStatus.Closed,

            ProjectStatus.Cancelled => false,

            ProjectStatus.Closed => false,

            _ => false
        };
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static ProjectResponse Map(Project project)
    {
        return new ProjectResponse(
            project.Id,

            project.CompanyId,
            project.ConstructionSite?.Company?.Name ?? "Hamilton Business Group",

            project.ConstructionSiteId,
            project.ConstructionSite?.Name ?? "Unknown Site",

            project.ProjectCode,
            project.Name,
            project.Description,

            project.PlannedStartDate,
            project.PlannedEndDate,
            project.ActualStartDate,
            project.ActualEndDate,

            project.ContractValue,
            project.ProgressPercentage,

            project.Status,
            project.CreatedAtUtc
        );
    }
}