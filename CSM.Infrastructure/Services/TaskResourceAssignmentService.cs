using CSM.Application.Projects.ResourceAssignments;
using CSM.Application.Projects.ResourceAssignments.Dtos;
using CSM.Domain.Entities.Projects;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;




namespace CSM.Infrastructure.Services;

public sealed class TaskResourceAssignmentService : ITaskResourceAssignmentService
{
    private readonly ApplicationDbContext _context;

    public TaskResourceAssignmentService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<TaskResourceAssignmentResponse>> GetByTaskAsync(
        Guid wbsTaskId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var access = await ProjectChildAccess.ForAsync(_context, currentUserId, false, false, cancellationToken);
        var task = await access.TaskAsync(wbsTaskId, cancellationToken);
        return await _context.TaskResourceAssignments
            .AsNoTracking()
            .Include(x => x.Employee)
            .Where(x => !x.IsDeleted && x.WbsTaskId == wbsTaskId && x.CompanyId == task.CompanyId && !x.Employee.IsDeleted && x.Employee.CompanyId == task.CompanyId)
            .Select(x => new TaskResourceAssignmentResponse
            {
                Id = x.Id,
                WbsTaskId = x.WbsTaskId,
                EmployeeId = x.EmployeeId,
                EmployeeName = x.Employee.FirstName + " " + x.Employee.LastName,
                Role = x.Role,
                PlannedHours = x.PlannedHours,
                ActualHours = x.ActualHours,
                AssignedDate = x.AssignedDate,
                Remarks = x.Remarks
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<TaskResourceAssignmentResponse> CreateAsync(
        CreateTaskResourceAssignmentRequest request,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var access = await ProjectChildAccess.ForAsync(_context, currentUserId, true, false, cancellationToken);
        var task = await access.TaskAsync(request.WbsTaskId, cancellationToken);
        var employee = await _context.Employees.FirstOrDefaultAsync(x => x.Id == request.EmployeeId && !x.IsDeleted && x.CompanyId == task.CompanyId, cancellationToken)
            ?? throw new ArgumentException("Invalid employee.");

        var entity = new TaskResourceAssignment
        {
            CompanyId = task.CompanyId,
            WbsTaskId = request.WbsTaskId,
            EmployeeId = request.EmployeeId,
            Role = request.Role,
            PlannedHours = request.PlannedHours,
            ActualHours = request.ActualHours,
            AssignedDate = request.AssignedDate,
            Remarks = request.Remarks
        };

        _context.TaskResourceAssignments.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return new TaskResourceAssignmentResponse
        {
            Id = entity.Id,
            WbsTaskId = entity.WbsTaskId,
            EmployeeId = entity.EmployeeId,
            EmployeeName = employee.FirstName + " " + employee.LastName,
            Role = entity.Role,
            PlannedHours = entity.PlannedHours,
            ActualHours = entity.ActualHours,
            AssignedDate = entity.AssignedDate,
            Remarks = entity.Remarks
        };
    }

    public async Task<TaskResourceAssignmentResponse> UpdateAsync(
        Guid id,
        UpdateTaskResourceAssignmentRequest request,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var access = await ProjectChildAccess.ForAsync(_context, currentUserId, true, false, cancellationToken);
        var entity = await _context.TaskResourceAssignments
            .Include(x => x.Employee)
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted && _context.WbsTasks.Any(t => t.Id == x.WbsTaskId && !t.IsDeleted && t.CompanyId == x.CompanyId && access.Projects.Any(p => p.Id == t.ProjectId && p.CompanyId == t.CompanyId)), cancellationToken)
            ?? throw new UnauthorizedAccessException("Access denied.");

        if (entity.Employee.IsDeleted || entity.Employee.CompanyId != entity.CompanyId) throw new ArgumentException("Invalid employee.");
        entity.Role = request.Role;
        entity.PlannedHours = request.PlannedHours;
        entity.ActualHours = request.ActualHours;
        entity.AssignedDate = request.AssignedDate;
        entity.Remarks = request.Remarks;

        await _context.SaveChangesAsync(cancellationToken);

        return new TaskResourceAssignmentResponse
        {
            Id = entity.Id,
            WbsTaskId = entity.WbsTaskId,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee.FirstName + " " + entity.Employee.LastName,
            Role = entity.Role,
            PlannedHours = entity.PlannedHours,
            ActualHours = entity.ActualHours,
            AssignedDate = entity.AssignedDate,
            Remarks = entity.Remarks
        };
    }

    public async Task DeleteAsync(
        Guid id,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var access = await ProjectChildAccess.ForAsync(_context, currentUserId, true, false, cancellationToken);
        var entity = await _context.TaskResourceAssignments
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted && _context.WbsTasks.Any(t => t.Id == x.WbsTaskId && !t.IsDeleted && t.CompanyId == x.CompanyId && access.Projects.Any(p => p.Id == t.ProjectId && p.CompanyId == t.CompanyId)), cancellationToken)
            ?? throw new UnauthorizedAccessException("Access denied.");

        _context.TaskResourceAssignments.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }
}