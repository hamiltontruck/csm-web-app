using System.Linq.Expressions;
using CSM.Application.Projects.WbsTasks;
using CSM.Application.Projects.WbsTasks.Dtos;
using CSM.Domain.Entities.Projects;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public sealed class WbsTaskService : IWbsTaskService
{
    private readonly ApplicationDbContext _context;

    public WbsTaskService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyCollection<WbsTaskResponse>> GetAllAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        return await _context.WbsTasks
            .AsNoTracking()
            .OrderBy(x => x.TaskCode)
            .Select(MapToResponse)
            .ToListAsync(cancellationToken);
    }

    public async Task<WbsTaskResponse?> GetByIdAsync(
        Guid currentUserId,
        Guid taskId,
        CancellationToken cancellationToken = default)
    {
        return await _context.WbsTasks
            .AsNoTracking()
            .Where(x => x.Id == taskId)
            .Select(MapToResponse)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<WbsTaskResponse> CreateAsync(
        Guid currentUserId,
        CreateWbsTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        var task = new WbsTask
        {
            CompanyId = Guid.Empty, // Replace with tenant later

            ProjectId = request.ProjectId,
            MilestoneId = request.MilestoneId,
            AssignedEmployeeId = request.AssignedEmployeeId,

            TaskCode = request.TaskCode,
            Name = request.Name,
            Description = request.Description,

            PlannedStartDate = request.PlannedStartDate,
            PlannedFinishDate = request.PlannedFinishDate,
            ActualStartDate = request.ActualStartDate,
            ActualFinishDate = request.ActualFinishDate,

            Status = request.Status,
            Priority = request.Priority,

            ProgressPercentage = request.ProgressPercentage,
            EstimatedHours = request.EstimatedHours,
            ActualHours = request.ActualHours
        };

        _context.WbsTasks.Add(task);
        await _context.SaveChangesAsync(cancellationToken);

        return MapToResponse.Compile()(task);
    }

    private static readonly Expression<Func<WbsTask, WbsTaskResponse>>
        MapToResponse = task => new WbsTaskResponse
        {
            Id = task.Id,
            CompanyId = task.CompanyId,

            ProjectId = task.ProjectId,
            MilestoneId = task.MilestoneId,
            AssignedEmployeeId = task.AssignedEmployeeId,

            TaskCode = task.TaskCode,
            Name = task.Name,
            Description = task.Description,

            PlannedStartDate = task.PlannedStartDate,
            PlannedFinishDate = task.PlannedFinishDate,
            ActualStartDate = task.ActualStartDate,
            ActualFinishDate = task.ActualFinishDate,

            Status = task.Status,
            Priority = task.Priority,

            ProgressPercentage = task.ProgressPercentage,
            EstimatedHours = task.EstimatedHours,
            ActualHours = task.ActualHours
        };
}