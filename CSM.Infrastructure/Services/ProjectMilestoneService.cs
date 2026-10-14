using CSM.Application.Projects.ProjectMilestones;
using CSM.Application.Projects.ProjectMilestones.Dtos;
using CSM.Domain.Entities.Projects;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace CSM.Infrastructure.Services;

public sealed class ProjectMilestoneService : IProjectMilestoneService
{
    private readonly ApplicationDbContext _context;

    public ProjectMilestoneService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyCollection<ProjectMilestoneResponse>> GetAllAsync(
        Guid currentUserId,
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Milestones
            .AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .OrderBy(x => x.PlannedDate)
            .Select(MapToResponse)
            .ToListAsync(cancellationToken);
    }

    public async Task<ProjectMilestoneResponse?> GetByIdAsync(
        Guid currentUserId,
        Guid milestoneId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Milestones
            .AsNoTracking()
            .Where(x => x.Id == milestoneId)
            .Select(MapToResponse)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ProjectMilestoneResponse> CreateAsync(
        Guid currentUserId,
        CreateProjectMilestoneRequest request,
        CancellationToken cancellationToken = default)
    {
        var project = await _context.Projects
            .FirstOrDefaultAsync(x => x.Id == request.ProjectId, cancellationToken);

        if (project is null)
            throw new Exception("Project not found.");

        var exists = await _context.Milestones.AnyAsync(x =>
            x.CompanyId == project.CompanyId &&
            x.ProjectId == request.ProjectId &&
            x.MilestoneNumber == request.MilestoneNumber,
            cancellationToken);

        if (exists)
            throw new Exception("A milestone with this milestone number already exists.");

        var milestone = new Milestone
        {
            CompanyId = project.CompanyId,
            ProjectId = request.ProjectId,
            MilestoneNumber = request.MilestoneNumber,
            Name = request.Name.Trim(),
            Description = request.Description,
            PlannedDate = request.PlannedDate,
            ActualDate = request.ActualDate,
            Status = request.Status,
            ProgressPercentage = request.ProgressPercentage,
            Notes = request.Notes
        };

        _context.Milestones.Add(milestone);
        await _context.SaveChangesAsync(cancellationToken);

        return MapToResponse.Compile()(milestone);
    }
private static readonly Expression<Func<Milestone, ProjectMilestoneResponse>>
    MapToResponse = m => new ProjectMilestoneResponse
    {
        Id = m.Id,
        CompanyId = m.CompanyId,
        ProjectId = m.ProjectId,
        ProjectPhaseId = m.ProjectPhaseId,
        PhaseName = m.ProjectPhase != null ? m.ProjectPhase.Name : null,
        MilestoneNumber = m.MilestoneNumber,
        Name = m.Name,
        Description = m.Description,
        PlannedDate = m.PlannedDate,
        ActualDate = m.ActualDate,
        Status = m.Status,
        ProgressPercentage = m.ProgressPercentage,
        Notes = m.Notes
    };
}