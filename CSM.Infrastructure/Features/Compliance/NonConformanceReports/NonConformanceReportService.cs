using CSM.Infrastructure.Features.Compliance;
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
        Guid currentUserId,
        CancellationToken ct = default)
    {
        var companyId = await ComplianceRecordAccess.CompanyAsync(_db, currentUserId, false, ct);

        return await _db.NonConformanceReports
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId && !x.IsDeleted && x.ConstructionSite.CompanyId == companyId && (x.ProjectId == null || (x.Project!.CompanyId == companyId && x.Project.ConstructionSiteId == x.ConstructionSiteId)) && (x.AssignedToEmployeeId == null || (x.AssignedToEmployee!.CompanyId == companyId)))
            .OrderByDescending(x => x.ReportedDate)
            .Select(Map())
            .ToListAsync(ct);
    }

    public async Task<NonConformanceReportResponse?> GetByIdAsync(
        Guid currentUserId,
        Guid id,
        CancellationToken ct = default)
    {
        var companyId = await ComplianceRecordAccess.CompanyAsync(_db, currentUserId, false, ct);

        return await _db.NonConformanceReports
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.Id == id && !x.IsDeleted && x.ConstructionSite.CompanyId == companyId && (x.ProjectId == null || (x.Project!.CompanyId == companyId && x.Project.ConstructionSiteId == x.ConstructionSiteId)) && (x.AssignedToEmployeeId == null || (x.AssignedToEmployee!.CompanyId == companyId)))
            .Select(Map())
            .FirstOrDefaultAsync(ct);
    }

    public async Task<NonConformanceReportResponse> CreateAsync(
        Guid userId,
        CreateNonConformanceReportRequest request,
        CancellationToken ct = default)
    {
        var companyId = await ComplianceRecordAccess.CompanyAsync(_db, userId, true, ct);
        await ComplianceRecordAccess.ParentsAsync(_db, companyId, request.ConstructionSiteId, request.ProjectId, request.AssignedToEmployeeId, ct);

        var entity = new NonConformanceReport
        {
            CompanyId = companyId,
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

        return (await GetByIdAsync(userId, entity.Id, ct))!;
    }

    public async Task<NonConformanceReportResponse> UpdateAsync(
        Guid currentUserId,
        Guid id,
        UpdateNonConformanceReportRequest request,
        CancellationToken ct = default)
    {
        var companyId = await ComplianceRecordAccess.CompanyAsync(_db, currentUserId, true, ct);

        var entity = await _db.NonConformanceReports
            .FirstOrDefaultAsync(
                x => x.CompanyId == companyId && x.Id == id && !x.IsDeleted,
                ct)
            ?? throw new KeyNotFoundException("NCR not found.");

        await ComplianceRecordAccess.ParentsAsync(_db, companyId, request.ConstructionSiteId, request.ProjectId, request.AssignedToEmployeeId, ct);

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

        return (await GetByIdAsync(currentUserId, id, ct))!;
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
            RaisedByName = x.RaisedBy.CompanyId == x.CompanyId ? x.RaisedBy.FirstName + " " + x.RaisedBy.LastName : string.Empty,

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