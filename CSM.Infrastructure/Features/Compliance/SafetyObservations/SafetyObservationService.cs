using CSM.Application.Features.Compliance.SafetyObservations.DTOs;
using CSM.Application.Features.Compliance.SafetyObservations.Services;
using CSM.Domain.Entities.Compliance;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Features.Compliance.SafetyObservations;

public sealed class SafetyObservationService : ISafetyObservationService
{
    private readonly ApplicationDbContext _db;

    public SafetyObservationService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<SafetyObservationListItem>> GetAllAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var user = await GetUser(currentUserId, cancellationToken);

        return await _db.SafetyObservations
            .Where(x => x.CompanyId == user.CompanyId && !x.IsDeleted)
            .Include(x => x.ConstructionSite)
            .OrderByDescending(x => x.ObservationDate)
            .Select(x => new SafetyObservationListItem
            {
                Id = x.Id,
                ObservationNumber = x.ObservationNumber,
                ConstructionSiteName = x.ConstructionSite.Name,
                ObservationDate = x.ObservationDate,
                Category = x.Category,
                RiskLevel = x.RiskLevel,
                Status = x.Status
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<SafetyObservationResponse> GetByIdAsync(
        Guid currentUserId,
        Guid observationId,
        CancellationToken cancellationToken = default)
    {
        var user = await GetUser(currentUserId, cancellationToken);

        var entity = await _db.SafetyObservations
            .Include(x => x.ConstructionSite)
            .Include(x => x.Project)
            .Include(x => x.Observer)
            .Include(x => x.Employee)
            .SingleOrDefaultAsync(
                x => x.Id == observationId &&
                     x.CompanyId == user.CompanyId &&
                     !x.IsDeleted,
                cancellationToken)
            ?? throw new Exception("Safety observation not found.");

        return Map(entity);
    }

    public async Task<SafetyObservationResponse> CreateAsync(
        Guid currentUserId,
        CreateSafetyObservationRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await GetUser(currentUserId, cancellationToken);

        var entity = new SafetyObservation
        {
            CompanyId = user.CompanyId
                ?? throw new InvalidOperationException("User is not assigned to a company."),
            ConstructionSiteId = request.ConstructionSiteId,
            ProjectId = request.ProjectId,
            ObserverId = currentUserId,
            EmployeeId = request.EmployeeId,


            ObservationNumber = request.ObservationNumber.Trim(),
            Category = request.Category,
            ObservationDate = request.ObservationDate,
            Location = request.Location.Trim(),
            Description = request.Description.Trim(),
            RiskLevel = request.RiskLevel,
            PositiveObservation = request.PositiveObservation,
            ImmediateAction = Clean(request.ImmediateAction),
            RecommendedAction = Clean(request.RecommendedAction),
            Status = ObservationStatus.Open
        };

        _db.SafetyObservations.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(currentUserId, entity.Id, cancellationToken);
    }

    public async Task<SafetyObservationResponse> UpdateAsync(
        Guid currentUserId,
        Guid observationId,
        UpdateSafetyObservationRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await GetUser(currentUserId, cancellationToken);

        var entity = await _db.SafetyObservations
            .Include(x => x.ConstructionSite)
            .Include(x => x.Project)
            .Include(x => x.Observer)
            .Include(x => x.Employee)
            .SingleOrDefaultAsync(
                x => x.Id == observationId &&
                     x.CompanyId == user.CompanyId &&
                     !x.IsDeleted,
                cancellationToken)
            ?? throw new Exception("Safety observation not found.");

        entity.ConstructionSiteId = request.ConstructionSiteId;
        entity.ProjectId = request.ProjectId;
        entity.EmployeeId = request.EmployeeId;
        entity.ObservationNumber = request.ObservationNumber.Trim();
        entity.Category = request.Category;
        entity.ObservationDate = request.ObservationDate;
        entity.Location = request.Location.Trim();
        entity.Description = request.Description.Trim();
        entity.RiskLevel = request.RiskLevel;
        entity.PositiveObservation = request.PositiveObservation;
        entity.ImmediateAction = Clean(request.ImmediateAction);
        entity.RecommendedAction = Clean(request.RecommendedAction);
        entity.Status = request.Status;
        entity.ClosedAtUtc = request.ClosedAtUtc;

        await _db.SaveChangesAsync(cancellationToken);

        return Map(entity);
    }

    private async Task<User> GetUser(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await _db.Users
            .SingleAsync(x => x.Id == id, cancellationToken);
    }

    private static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Trim();
    }

    private static SafetyObservationResponse Map(
        SafetyObservation x)
    {
        return new SafetyObservationResponse
        {
            Id = x.Id,
            CompanyId = x.CompanyId,
            ConstructionSiteId = x.ConstructionSiteId,
            ConstructionSiteName = x.ConstructionSite.Name,
            ProjectId = x.ProjectId,
            ProjectName = x.Project?.Name,
            ObserverId = x.ObserverId,
            ObserverName = x.Observer.Email,
            EmployeeId = x.EmployeeId,
            EmployeeName = x.Employee == null
                ? null
                : $"{x.Employee.FirstName} {x.Employee.LastName}",
            ObservationNumber = x.ObservationNumber,
            Category = x.Category,
            ObservationDate = x.ObservationDate,
            Location = x.Location,
            Description = x.Description,
            RiskLevel = x.RiskLevel,
            PositiveObservation = x.PositiveObservation,
            ImmediateAction = x.ImmediateAction,
            RecommendedAction = x.RecommendedAction,
            Status = x.Status,
            ClosedAtUtc = x.ClosedAtUtc,
            CreatedAtUtc = x.CreatedAtUtc,
            UpdatedAtUtc = x.UpdatedAtUtc
        };
    }
}