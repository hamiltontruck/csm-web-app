using CSM.Application.Common.Exceptions;
using CSM.Application.Compliance.SafetyIncidents;
using CSM.Application.Compliance.SafetyIncidents.Dtos;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Compliance;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public sealed class SafetyIncidentService : ISafetyIncidentService
{
    private readonly ApplicationDbContext _dbContext;

    public SafetyIncidentService(
        ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<SafetyIncidentResponse>> GetAllAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanManage(actor);

        var query = _dbContext.SafetyIncidents
            .AsNoTracking()
            .Include(x => x.ConstructionSite)
            .Where(x => !x.IsDeleted);

        if (!IsSuperAdmin(actor))
        {
            query = query.Where(
                x => x.CompanyId == GetActorCompanyId(actor));
        }

        var incidents = await query
            .OrderByDescending(x => x.OccurredAtUtc)
            .ToListAsync(cancellationToken);

        return incidents.Select(Map).ToList();
    }

    public async Task<SafetyIncidentResponse> GetByIdAsync(
        Guid currentUserId,
        Guid safetyIncidentId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanManage(actor);

        var incident = await _dbContext.SafetyIncidents
            .AsNoTracking()
            .Include(x => x.ConstructionSite)
            .SingleOrDefaultAsync(
                x => x.Id == safetyIncidentId &&
                     !x.IsDeleted,
                cancellationToken)
            ?? throw new SafetyIncidentManagementException(
                "Safety incident was not found.");

        EnsureCompanyAccess(actor, incident.CompanyId);

        return Map(incident);
    }

    public async Task<SafetyIncidentResponse> CreateAsync(
        Guid currentUserId,
        CreateSafetyIncidentRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanManage(actor);

        var companyId = GetActorCompanyId(actor);

        Validate(
            request.IncidentNumber,
            request.OccurredAtUtc,
            request.LocationDescription,
            request.Description,
            request.Severity,
            request.Status,
            request.InjuryOccurred,
            request.MedicalTreatmentRequired,
            request.LostTimeIncident,
            request.InvestigationCompletedAtUtc,
            request.ClosedAtUtc);

        await EnsureConstructionSiteAccess(
            companyId,
            request.ConstructionSiteId,
            cancellationToken);

        await EnsureProjectAccess(
            companyId,
            request.ProjectId,
            cancellationToken);

        await EnsureEmployeeAccess(
            companyId,
            request.EmployeeId,
            cancellationToken);

        await EnsureEmployeeAccess(
            companyId,
            request.InvestigatedBy,
            cancellationToken);

        var normalizedIncidentNumber =
            request.IncidentNumber.Trim();

        var duplicate = await _dbContext.SafetyIncidents
            .AnyAsync(
                x =>
                    !x.IsDeleted &&
                    x.CompanyId == companyId &&
                    x.IncidentNumber == normalizedIncidentNumber,
                cancellationToken);

        if (duplicate)
        {
            throw new SafetyIncidentManagementException(
                "A safety incident with this incident number already exists in the company.");
        }

        var incident = new SafetyIncident
        {
            CompanyId = companyId,
            ConstructionSiteId = request.ConstructionSiteId,
            ProjectId = request.ProjectId,
            EmployeeId = request.EmployeeId,
            IncidentNumber = normalizedIncidentNumber,
            OccurredAtUtc = request.OccurredAtUtc,
            ReportedAtUtc = DateTime.UtcNow,
            ReportedBy = currentUserId,
            LocationDescription = request.LocationDescription.Trim(),
            Description = request.Description.Trim(),
            Severity = request.Severity,
            Status = request.Status,
            InjuryOccurred = request.InjuryOccurred,
            MedicalTreatmentRequired =
                request.MedicalTreatmentRequired,
            LostTimeIncident = request.LostTimeIncident,
            ImmediateActionTaken =
                Clean(request.ImmediateActionTaken),
            RootCause = Clean(request.RootCause),
            CorrectiveAction = Clean(request.CorrectiveAction),
            InvestigatedBy = request.InvestigatedBy,
            InvestigationCompletedAtUtc =
                request.InvestigationCompletedAtUtc,
            ClosedAtUtc = request.ClosedAtUtc
        };

        _dbContext.SafetyIncidents.Add(incident);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(incident);
    }

    public async Task<SafetyIncidentResponse> UpdateAsync(
        Guid currentUserId,
        Guid safetyIncidentId,
        UpdateSafetyIncidentRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanManage(actor);

        var incident = await _dbContext.SafetyIncidents
            .SingleOrDefaultAsync(
                x =>
                    x.Id == safetyIncidentId &&
                    !x.IsDeleted,
                cancellationToken)
            ?? throw new SafetyIncidentManagementException(
                "Safety incident was not found.");

        EnsureCompanyAccess(
            actor,
            incident.CompanyId);

        Validate(
            request.IncidentNumber,
            request.OccurredAtUtc,
            request.LocationDescription,
            request.Description,
            request.Severity,
            request.Status,
            request.InjuryOccurred,
            request.MedicalTreatmentRequired,
            request.LostTimeIncident,
            request.InvestigationCompletedAtUtc,
            request.ClosedAtUtc);

        await EnsureConstructionSiteAccess(
            incident.CompanyId,
            request.ConstructionSiteId,
            cancellationToken);

        await EnsureProjectAccess(
            incident.CompanyId,
            request.ProjectId,
            cancellationToken);

        await EnsureEmployeeAccess(
            incident.CompanyId,
            request.EmployeeId,
            cancellationToken);

        await EnsureEmployeeAccess(
            incident.CompanyId,
            request.InvestigatedBy,
            cancellationToken);

        var normalizedIncidentNumber =
            request.IncidentNumber.Trim();

        var duplicate = await _dbContext.SafetyIncidents
            .AnyAsync(
                x =>
                    x.Id != incident.Id &&
                    !x.IsDeleted &&
                    x.CompanyId == incident.CompanyId &&
                    x.IncidentNumber == normalizedIncidentNumber,
                cancellationToken);

        if (duplicate)
        {
            throw new SafetyIncidentManagementException(
                "A safety incident with this incident number already exists in the company.");
        }

        incident.ConstructionSiteId =
            request.ConstructionSiteId;
        incident.ProjectId = request.ProjectId;
        incident.EmployeeId = request.EmployeeId;
        incident.IncidentNumber =
            normalizedIncidentNumber;
        incident.OccurredAtUtc =
            request.OccurredAtUtc;
        incident.ReportedAtUtc =
            request.ReportedAtUtc;
        incident.LocationDescription =
            request.LocationDescription.Trim();
        incident.Description =
            request.Description.Trim();
        incident.Severity = request.Severity;
        incident.Status = request.Status;
        incident.InjuryOccurred =
            request.InjuryOccurred;
        incident.MedicalTreatmentRequired =
            request.MedicalTreatmentRequired;
        incident.LostTimeIncident =
            request.LostTimeIncident;
        incident.ImmediateActionTaken =
            Clean(request.ImmediateActionTaken);
        incident.RootCause =
            Clean(request.RootCause);
        incident.CorrectiveAction =
            Clean(request.CorrectiveAction);
        incident.InvestigatedBy =
            request.InvestigatedBy;
        incident.InvestigationCompletedAtUtc =
            request.InvestigationCompletedAtUtc;
        incident.ClosedAtUtc =
            request.ClosedAtUtc;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(incident);
    }

    private async Task<User> GetActorAsync(
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Users
            .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(
                x => x.Id == currentUserId,
                cancellationToken)
            ?? throw new SafetyIncidentManagementException(
                "Authenticated user was not found.");
    }

    private static void EnsureCanManage(
        User actor)
    {
        if (IsSuperAdmin(actor) ||
            HasRole(actor, AppRoles.CompanyAdmin) ||
            HasRole(actor, AppRoles.SiteManager))
        {
            return;
        }

        throw new SafetyIncidentManagementException(
            "You are not authorized to manage safety incidents.");
    }

    private static void EnsureCompanyAccess(
        User actor,
        Guid companyId)
    {
        if (IsSuperAdmin(actor))
        {
            return;
        }

        if (!actor.CompanyId.HasValue ||
            actor.CompanyId.Value != companyId)
        {
            throw new SafetyIncidentManagementException(
                "You are not authorized to access this safety incident.");
        }
    }

    private static Guid GetActorCompanyId(
        User actor)
    {
        if (!actor.CompanyId.HasValue)
        {
            throw new SafetyIncidentManagementException(
                "Current user is not assigned to a company.");
        }

        return actor.CompanyId.Value;
    }

    private async Task EnsureConstructionSiteAccess(
        Guid companyId,
        Guid constructionSiteId,
        CancellationToken cancellationToken)
    {
        var exists = await _dbContext.ConstructionSites
            .AnyAsync(
                x =>
                    x.Id == constructionSiteId &&
                    x.CompanyId == companyId &&
                    !x.IsDeleted,
                cancellationToken);

        if (!exists)
        {
            throw new SafetyIncidentManagementException(
                "The selected construction site is invalid or inaccessible.");
        }
    }

    private async Task EnsureProjectAccess(
        Guid companyId,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        if (!projectId.HasValue)
        {
            return;
        }

        var exists = await _dbContext.Projects
            .AnyAsync(
                x =>
                    x.Id == projectId.Value &&
                    x.CompanyId == companyId &&
                    !x.IsDeleted,
                cancellationToken);

        if (!exists)
        {
            throw new SafetyIncidentManagementException(
                "The selected project is invalid or inaccessible.");
        }
    }

    private async Task EnsureEmployeeAccess(
        Guid companyId,
        Guid? employeeId,
        CancellationToken cancellationToken)
    {
        if (!employeeId.HasValue)
        {
            return;
        }

        var exists = await _dbContext.Employees
            .AnyAsync(
                x =>
                    x.Id == employeeId.Value &&
                    x.CompanyId == companyId &&
                    !x.IsDeleted,
                cancellationToken);

        if (!exists)
        {
            throw new SafetyIncidentManagementException(
                "The selected employee is invalid or inaccessible.");
        }
    }

    private static void Validate(
        string incidentNumber,
        DateTime occurredAtUtc,
        string locationDescription,
        string description,
        SafetyIncidentSeverity severity,
        SafetyIncidentStatus status,
        bool injuryOccurred,
        bool medicalTreatmentRequired,
        bool lostTimeIncident,
        DateTime? investigationCompletedAtUtc,
        DateTime? closedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(incidentNumber))
        {
            throw new SafetyIncidentManagementException(
                "Incident number is required.");
        }

        if (incidentNumber.Trim().Length > 50)
        {
            throw new SafetyIncidentManagementException(
                "Incident number cannot exceed 50 characters.");
        }

        if (occurredAtUtc == default)
        {
            throw new SafetyIncidentManagementException(
                "Incident occurrence date is required.");
        }

        if (string.IsNullOrWhiteSpace(locationDescription))
        {
            throw new SafetyIncidentManagementException(
                "Location description is required.");
        }

        if (locationDescription.Trim().Length > 500)
        {
            throw new SafetyIncidentManagementException(
                "Location description cannot exceed 500 characters.");
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new SafetyIncidentManagementException(
                "Incident description is required.");
        }

        if (description.Trim().Length > 4000)
        {
            throw new SafetyIncidentManagementException(
                "Incident description cannot exceed 4000 characters.");
        }

        if (!Enum.IsDefined(severity))
        {
            throw new SafetyIncidentManagementException(
                "Safety incident severity is invalid.");
        }

        if (!Enum.IsDefined(status))
        {
            throw new SafetyIncidentManagementException(
                "Safety incident status is invalid.");
        }

        if (medicalTreatmentRequired && !injuryOccurred)
        {
            throw new SafetyIncidentManagementException(
                "Medical treatment cannot be required when no injury occurred.");
        }

        if (lostTimeIncident && !injuryOccurred)
        {
            throw new SafetyIncidentManagementException(
                "A lost-time incident requires an injury to have occurred.");
        }

        if (investigationCompletedAtUtc.HasValue &&
            investigationCompletedAtUtc.Value < occurredAtUtc)
        {
            throw new SafetyIncidentManagementException(
                "Investigation completion cannot be earlier than incident occurrence.");
        }

        if (closedAtUtc.HasValue &&
            closedAtUtc.Value < occurredAtUtc)
        {
            throw new SafetyIncidentManagementException(
                "Closure cannot be earlier than incident occurrence.");
        }

        if (status == SafetyIncidentStatus.Closed &&
            !closedAtUtc.HasValue)
        {
            throw new SafetyIncidentManagementException(
                "A closed safety incident must have a closure date.");
        }

        if (status == SafetyIncidentStatus.UnderInvestigation &&
            !investigationCompletedAtUtc.HasValue &&
            !string.IsNullOrWhiteSpace(description))
        {
            // Valid state: investigation may still be in progress.
        }
    }

    private static bool IsSuperAdmin(
        User actor)
    {
        return HasRole(
            actor,
            AppRoles.SuperAdmin);
    }

    private static bool HasRole(
        User actor,
        string roleName)
    {
        return actor.UserRoles.Any(
            x =>
                !x.IsDeleted &&
                !x.Role.IsDeleted &&
                string.Equals(
                    x.Role.Name,
                    roleName,
                    StringComparison.OrdinalIgnoreCase));
    }

    private static string? Clean(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static SafetyIncidentResponse Map(
        SafetyIncident incident)
    {
        return new SafetyIncidentResponse
        {
            Id = incident.Id,
            CompanyId = incident.CompanyId,
            ConstructionSiteId =
                incident.ConstructionSiteId,
            ConstructionSiteName = incident.ConstructionSite?.Name ?? string.Empty,
            ProjectId = incident.ProjectId,
            EmployeeId = incident.EmployeeId,
            IncidentNumber =
                incident.IncidentNumber,
            OccurredAtUtc =
                incident.OccurredAtUtc,
            ReportedAtUtc =
                incident.ReportedAtUtc,
            ReportedBy =
                incident.ReportedBy,
            LocationDescription =
                incident.LocationDescription,
            Description =
                incident.Description,
            Severity =
                incident.Severity,
            Status =
                incident.Status,
            InjuryOccurred =
                incident.InjuryOccurred,
            MedicalTreatmentRequired =
                incident.MedicalTreatmentRequired,
            LostTimeIncident =
                incident.LostTimeIncident,
            ImmediateActionTaken =
                incident.ImmediateActionTaken,
            RootCause =
                incident.RootCause,
            CorrectiveAction =
                incident.CorrectiveAction,
            InvestigatedBy =
                incident.InvestigatedBy,
            InvestigationCompletedAtUtc =
                incident.InvestigationCompletedAtUtc,
            ClosedAtUtc =
                incident.ClosedAtUtc,
            CreatedAtUtc =
                incident.CreatedAtUtc,
            UpdatedAtUtc =
                incident.UpdatedAtUtc
        };
    }
}
