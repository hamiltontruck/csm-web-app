using CSM.Application.Projects.ProjectDelays;
using CSM.Application.Projects.ProjectDelays.Dtos;
using CSM.Domain.Entities.Projects;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
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
        Guid tenantId,
        Guid? projectId,
        Guid? siteId,
        CancellationToken cancellationToken = default)
    {
        var query = _context.ProjectDelays
            .AsNoTracking()
            .Where(x => !x.IsDeleted);

        if (projectId.HasValue)
            query = query.Where(x => x.ProjectId == projectId.Value);

        return await query
            .OrderByDescending(x => x.DelayDate)
            .Select(MapToResponse)
            .ToListAsync(cancellationToken);
    }

    public async Task<ProjectDelayResponse?> GetByIdAsync(
        Guid tenantId,
        Guid delayId,
        CancellationToken cancellationToken = default)
    {
        return await _context.ProjectDelays
            .AsNoTracking()
            .Where(x => x.Id == delayId && !x.IsDeleted)
            .Select(MapToResponse)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ProjectDelayResponse> CreateAsync(
        Guid tenantId,
        CreateProjectDelayRequest request,
        CancellationToken cancellationToken = default)
    {
        var delay = new ProjectDelay
        {
            CompanyId = tenantId,
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
        Guid tenantId,
        Guid delayId,
        UpdateProjectDelayRequest request,
        CancellationToken cancellationToken = default)
    {
        var delay = await _context.ProjectDelays
            .FirstOrDefaultAsync(
                x => x.Id == delayId && !x.IsDeleted,
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
        Guid tenantId,
        Guid delayId,
        CancellationToken cancellationToken = default)
    {
        var delay = await _context.ProjectDelays
            .FirstOrDefaultAsync(
                x => x.Id == delayId && !x.IsDeleted,
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

    private static readonly Expression<Func<ProjectDelay, ProjectDelayResponse>>
        MapToResponse =
            delay => new ProjectDelayResponse(
                delay.Id,
                delay.ProjectId,
                Guid.Empty,
                DateOnly.FromDateTime(delay.DelayDate),
                delay.Category == "Weather" ? DelayCategory.Weather :
                delay.Category == "Material" ? DelayCategory.Material :
                delay.Category == "Labor" ? DelayCategory.Labor :
                delay.Category == "Equipment" ? DelayCategory.Equipment :
                delay.Category == "Client" ? DelayCategory.Client :
                delay.Category == "Design" ? DelayCategory.Design :
                delay.Category == "Permit" ? DelayCategory.Permit :
                DelayCategory.Other,
                delay.Title,
                delay.Description,
                delay.DelayDays,
                0m,
                delay.ActionTaken,
                delay.IsResolved);
}