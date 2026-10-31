using CSM.Application.Projects.ProjectDelays;
using CSM.Application.Projects.ProjectDelays.Dtos;
using CSM.Domain.Entities.Projects;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using CSM.Domain.Enums;

namespace CSM.Infrastructure.Services;

public sealed class ProjectDelayService : IProjectDelayService
{
    private readonly ApplicationDbContext _context;

    public ProjectDelayService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyCollection<ProjectDelayResponse>> GetAllAsync(
        Guid currentUserId,
        Guid? projectId,
        Guid? siteId,
        CancellationToken cancellationToken = default)
    {
        var access = await ProjectChildAccess.ForAsync(_context, currentUserId, false, false, cancellationToken);
        var query = _context.ProjectDelays
            .AsNoTracking()
            .Where(x => !x.IsDeleted && access.Projects.Any(p => p.Id == x.ProjectId && p.CompanyId == x.CompanyId));

        if (siteId.HasValue)
            query = query.Where(x => access.Projects.Any(p => p.Id == x.ProjectId && p.ConstructionSiteId == siteId.Value));

        if (projectId.HasValue)
            query = query.Where(x => x.ProjectId == projectId.Value);

        // DelayDate is a SQL date stored on a DateTime property. Convert only
        // after the authorized query runs; Npgsql cannot translate this conversion.
        var delays = await query
            .OrderByDescending(x => x.DelayDate)
            .ToListAsync(cancellationToken);
        return delays.Select(Map).ToList();
    }

    public async Task<ProjectDelayResponse?> GetByIdAsync(
        Guid currentUserId,
        Guid delayId,
        CancellationToken cancellationToken = default)
    {
        var access = await ProjectChildAccess.ForAsync(_context, currentUserId, false, false, cancellationToken);
        var delay = await _context.ProjectDelays
            .AsNoTracking()
            .Where(x => x.Id == delayId && !x.IsDeleted && access.Projects.Any(p => p.Id == x.ProjectId && p.CompanyId == x.CompanyId))
            .FirstOrDefaultAsync(cancellationToken);
        return delay is null ? null : Map(delay);
    }

    public async Task<ProjectDelayResponse> CreateAsync(
        Guid currentUserId,
        CreateProjectDelayRequest request,
        CancellationToken cancellationToken = default)
    {
        var access = await ProjectChildAccess.ForAsync(_context, currentUserId, true, false, cancellationToken);
        var parent = await access.ProjectAsync(request.ProjectId, cancellationToken);
        var delay = new ProjectDelay
        {
            CompanyId = currentUserId,
            ProjectId = request.ProjectId,
            DelayDate = request.DelayDate.ToDateTime(TimeOnly.MinValue),
            Category = request.Category.ToString(),
            Title = request.Title,
            Description = request.Description,
            DelayDays = request.TimeImpactDays,
            ActionTaken = request.MitigationAction,
            IsResolved = false
        };

        _context.ProjectDelays.Add(delay);
        await _context.SaveChangesAsync(cancellationToken);

        return Map(delay);
    }

    public async Task<ProjectDelayResponse?> UpdateAsync(
        Guid currentUserId,
        Guid delayId,
        UpdateProjectDelayRequest request,
        CancellationToken cancellationToken = default)
    {
        var access = await ProjectChildAccess.ForAsync(_context, currentUserId, true, false, cancellationToken);
        var delay = await _context.ProjectDelays
            .FirstOrDefaultAsync(
                x => x.Id == delayId && !x.IsDeleted && access.Projects.Any(p => p.Id == x.ProjectId && p.CompanyId == x.CompanyId),
                cancellationToken);

        if (delay is null)
            return null;

        delay.DelayDate = request.DelayDate.ToDateTime(TimeOnly.MinValue);
        delay.Category = request.Category.ToString();
        delay.Title = request.Title;
        delay.Description = request.Description;
        delay.DelayDays = request.TimeImpactDays;
        delay.ActionTaken = request.MitigationAction;
        delay.IsResolved = request.IsResolved;
        delay.ResolvedDate = request.IsResolved
            ? DateTime.UtcNow
            : null;

        await _context.SaveChangesAsync(cancellationToken);

        return Map(delay);
    }

    public async Task<bool> DeleteAsync(
        Guid currentUserId,
        Guid delayId,
        CancellationToken cancellationToken = default)
    {
        var access = await ProjectChildAccess.ForAsync(_context, currentUserId, true, false, cancellationToken);
        var delay = await _context.ProjectDelays
            .FirstOrDefaultAsync(
                x => x.Id == delayId && !x.IsDeleted && access.Projects.Any(p => p.Id == x.ProjectId && p.CompanyId == x.CompanyId),
                cancellationToken);

        if (delay is null)
            return false;

        _context.ProjectDelays.Remove(delay);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static ProjectDelayResponse Map(ProjectDelay delay) =>
        new(
            delay.Id,
            delay.ProjectId,
            Guid.Empty,
            DateOnly.FromDateTime(delay.DelayDate),
            Enum.TryParse<CSM.Domain.Enums.DelayCategory>(
                delay.Category,
                out var category)
                ? category
                : CSM.Domain.Enums.DelayCategory.Other,
            delay.Title,
            delay.Description,
            delay.DelayDays,
            0m,
            delay.ActionTaken,
            delay.IsResolved);

}
