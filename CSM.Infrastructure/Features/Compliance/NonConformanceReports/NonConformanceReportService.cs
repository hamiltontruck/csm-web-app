using System.Linq.Expressions;
using CSM.Application.Features.Compliance.NonConformanceReports;
using CSM.Domain.Entities.Compliance;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;


namespace CSM.Infrastructure.Features.Compliance.NonConformanceReports;

public class NonConformanceReportService
{
    private readonly ApplicationDbContext _db;

    public NonConformanceReportService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<NonConformanceReportResponse>> GetAllAsync(
        Guid companyId,
        CancellationToken ct = default)
    {
        return await _db.NonConformanceReports
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .OrderByDescending(x => x.ReportedDate)
            .Select(Map())
            .ToListAsync(ct);
    }

    public async Task<NonConformanceReportResponse?> GetByIdAsync(
        Guid companyId,
        Guid id,
        CancellationToken ct = default)
    {
        return await _db.NonConformanceReports
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.Id == id)
            .Select(Map())
            .FirstOrDefaultAsync(ct);
    }

    public async Task<NonConformanceReportResponse> CreateAsync(
        Guid userId,
        CreateNonConformanceReportRequest request,
        CancellationToken ct = default)
    {
        var user = await GetUser(userId, ct);

        var entity = new NonConformanceReport
        {
            CompanyId = user.CompanyId
                ?? throw new InvalidOperationException("User has no CompanyId."),
            ConstructionSiteId = request.ConstructionSiteId,
            ProjectId = request.ProjectId,
            AssignedToEmployeeId = request.AssignedToEmployeeId,
            RaisedById = userId,

            NcrNumber = request.NcrNumber.Trim(),
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Location = request.Location.Trim(),

            Severity = request.Severity,
            Status = NcrStatus.Open,

            ReportedDate = request.ReportedDate,
            DueDate = request.DueDate
        };

        _db.NonConformanceReports.Add(entity);
        await _db.SaveChangesAsync(ct);

        return (await GetByIdAsync(user.CompanyId!.Value, entity.Id, ct))!;
    }

    public async Task<NonConformanceReportResponse> UpdateAsync(
        Guid companyId,
        Guid id,
        UpdateNonConformanceReportRequest request,
        CancellationToken ct = default)
    {
        var entity = await _db.NonConformanceReports
            .FirstOrDefaultAsync(
                x => x.CompanyId == companyId && x.Id == id,
                ct)
            ?? throw new KeyNotFoundException("NCR not found.");

        entity.ConstructionSiteId = request.ConstructionSiteId;
        entity.ProjectId = request.ProjectId;
        entity.AssignedToEmployeeId = request.AssignedToEmployeeId;

        entity.NcrNumber = request.NcrNumber.Trim();
        entity.Title = request.Title.Trim();
        entity.Description = request.Description.Trim();
        entity.Location = request.Location.Trim();

        entity.Severity = request.Severity;
        entity.Status = request.Status;

        entity.ReportedDate = request.ReportedDate;
        entity.DueDate = request.DueDate;
        entity.ClosedDate = request.ClosedDate;

        entity.RootCause = request.RootCause;
        entity.CorrectiveAction = request.CorrectiveAction;
        entity.PreventiveAction = request.PreventiveAction;

        await _db.SaveChangesAsync(ct);

        return (await GetByIdAsync(companyId, id, ct))!;
    }

    private async Task<User> GetUser(Guid id, CancellationToken ct)
    {
        return await _db.Users.FirstAsync(x => x.Id == id, ct);
    }

    private static Expression<Func<NonConformanceReport, NonConformanceReportResponse>> Map()
    {
        return x => new NonConformanceReportResponse
        {
            Id = x.Id,
            CompanyId = x.CompanyId,

            ConstructionSiteId = x.ConstructionSiteId,
            ConstructionSiteName = x.ConstructionSite.Name,

            ProjectId = x.ProjectId,
            ProjectName = x.Project != null ? x.Project.Name : null,

            RaisedById = x.RaisedById,
            RaisedByName = x.RaisedBy.FirstName + " " + x.RaisedBy.LastName,

            AssignedToEmployeeId = x.AssignedToEmployeeId,
            AssignedToEmployeeName = x.AssignedToEmployee != null
                ? x.AssignedToEmployee.FirstName + " " + x.AssignedToEmployee.LastName
                : null,

            NcrNumber = x.NcrNumber,
            Title = x.Title,
            Description = x.Description,
            Location = x.Location,

            Severity = x.Severity,
            Status = x.Status,

            ReportedDate = x.ReportedDate,
            DueDate = x.DueDate,
            ClosedDate = x.ClosedDate,

            RootCause = x.RootCause,
            CorrectiveAction = x.CorrectiveAction,
            PreventiveAction = x.PreventiveAction,

            CreatedAtUtc = x.CreatedAtUtc,
            UpdatedAtUtc = x.UpdatedAtUtc
        };
    }
}