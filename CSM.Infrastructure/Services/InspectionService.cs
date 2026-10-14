using CSM.Application.Common.Exceptions;
using CSM.Application.Compliance.Inspections;
using CSM.Application.Compliance.Inspections.Dtos;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Compliance;
using CSM.Domain.Entities.Identity;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public sealed class InspectionService : IInspectionService
{
    private readonly ApplicationDbContext _dbContext;

    public InspectionService(
        ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<InspectionResponse>> GetAllAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanManage(actor);

        var query = _dbContext.Inspections
            .AsNoTracking()
            .Include(x => x.ConstructionSite)
            .Include(x => x.ChecklistItems)
            .Where(x => !x.IsDeleted);

        if (!IsSuperAdmin(actor))
        {
            query = query.Where(
                x => x.CompanyId == GetActorCompanyId(actor));
        }

        var inspections = await query
            .OrderByDescending(x => x.ScheduledAtUtc)
            .ToListAsync(cancellationToken);

        return inspections
            .Select(Map)
            .ToList();
    }

    public async Task<InspectionResponse> GetByIdAsync(
        Guid currentUserId,
        Guid inspectionId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanManage(actor);

        var inspection = await _dbContext.Inspections
            .AsNoTracking()
            .Include(x => x.ConstructionSite)
            .Include(x => x.ChecklistItems)
            .SingleOrDefaultAsync(
                x =>
                    x.Id == inspectionId &&
                    !x.IsDeleted,
                cancellationToken)
            ?? throw new InspectionManagementException(
                "Inspection was not found.");

        EnsureCompanyAccess(
            actor,
            inspection.CompanyId);

        return Map(inspection);
    }

    public async Task<InspectionResponse> CreateAsync(
        Guid currentUserId,
        CreateInspectionRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanManage(actor);

        var companyId = GetActorCompanyId(actor);

        ValidateInspection(
            request.InspectionNumber,
            request.InspectionType,
            request.Title,
            request.ScheduledAtUtc,
            null,
            null,
            request.CorrectiveActionDueDate,
        null,
        request.Status,
        null);

        ValidateChecklistItems(
            request.ChecklistItems);

        await EnsureConstructionSiteAccess(
            companyId,
            request.ConstructionSiteId,
            cancellationToken);

        await EnsureProjectAccess(
            companyId,
            request.ProjectId,
            cancellationToken);

        await EnsureInspectorEmployeeAccess(
            companyId,
            request.InspectorEmployeeId,
            cancellationToken);

        var inspectionNumber =
            request.InspectionNumber.Trim();

        var duplicate = await _dbContext.Inspections
            .AnyAsync(
                x =>
                    !x.IsDeleted &&
                    x.CompanyId == companyId &&
                    x.InspectionNumber == inspectionNumber,
                cancellationToken);

        if (duplicate)
        {
            throw new InspectionManagementException(
                "An inspection with this inspection number already exists in the company.");
        }

        var inspection = new Inspection
        {
            CompanyId = companyId,
            ConstructionSiteId =
                request.ConstructionSiteId,
            ProjectId = request.ProjectId,
            InspectionNumber = inspectionNumber,
            InspectionType =
                request.InspectionType.Trim(),
            Title = request.Title.Trim(),
            ScheduledAtUtc =
                request.ScheduledAtUtc,
            InspectorEmployeeId =
                request.InspectorEmployeeId,
            ExternalInspectorName =
                Clean(request.ExternalInspectorName),
            ExternalOrganization =
                Clean(request.ExternalOrganization),
            Status = request.Status,
            Summary = Clean(request.Summary),
            CorrectiveActionRequired =
                Clean(request.CorrectiveActionRequired),
            CorrectiveActionDueDate =
                request.CorrectiveActionDueDate
        };

        foreach (var item in request.ChecklistItems)
        {
            inspection.ChecklistItems.Add(
                new InspectionChecklistItem
                {
                    CompanyId = companyId,
                    Sequence = item.Sequence,
                    Requirement = item.Requirement.Trim(),
                    IsCompliant = item.IsCompliant,
                    Observation = Clean(item.Observation),
                    CorrectiveAction =
                        Clean(item.CorrectiveAction),
                    DueDate = item.DueDate,
                    IsResolved = item.IsResolved
                });
        }

        _dbContext.Inspections.Add(inspection);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(inspection);
    }

    public async Task<InspectionResponse> UpdateAsync(
        Guid currentUserId,
        Guid inspectionId,
        UpdateInspectionRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanManage(actor);

        var inspection = await _dbContext.Inspections
            .Include(x => x.ChecklistItems)
            .SingleOrDefaultAsync(
                x =>
                    x.Id == inspectionId &&
                    !x.IsDeleted,
                cancellationToken)
            ?? throw new InspectionManagementException(
                "Inspection was not found.");

        EnsureCompanyAccess(
            actor,
            inspection.CompanyId);

        ValidateInspection(
            request.InspectionNumber,
            request.InspectionType,
            request.Title,
            request.ScheduledAtUtc,
            request.StartedAtUtc,
            request.CompletedAtUtc,
            request.CorrectiveActionDueDate,
        request.SignedOffAtUtc,
        request.Status,
        inspection);

        ValidateChecklistItems(
            request.ChecklistItems);

        await EnsureConstructionSiteAccess(
            inspection.CompanyId,
            request.ConstructionSiteId,
            cancellationToken);

        await EnsureProjectAccess(
            inspection.CompanyId,
            request.ProjectId,
            cancellationToken);

        await EnsureInspectorEmployeeAccess(
            inspection.CompanyId,
            request.InspectorEmployeeId,
            cancellationToken);

        var inspectionNumber =
            request.InspectionNumber.Trim();

        var duplicate = await _dbContext.Inspections
            .AnyAsync(
                x =>
                    x.Id != inspection.Id &&
                    !x.IsDeleted &&
                    x.CompanyId == inspection.CompanyId &&
                    x.InspectionNumber == inspectionNumber,
                cancellationToken);

        if (duplicate)
        {
            throw new InspectionManagementException(
                "An inspection with this inspection number already exists in the company.");
        }

        inspection.ConstructionSiteId =
            request.ConstructionSiteId;

        inspection.ProjectId =
            request.ProjectId;

        inspection.InspectionNumber =
            inspectionNumber;

        inspection.InspectionType =
            request.InspectionType.Trim();

        inspection.Title =
            request.Title.Trim();

        inspection.ScheduledAtUtc =
            request.ScheduledAtUtc;

        inspection.StartedAtUtc =
            request.StartedAtUtc;

        inspection.CompletedAtUtc =
            request.CompletedAtUtc;

        inspection.InspectorEmployeeId =
            request.InspectorEmployeeId;

        inspection.ExternalInspectorName =
            Clean(request.ExternalInspectorName);

        inspection.ExternalOrganization =
            Clean(request.ExternalOrganization);

        inspection.Status =
            request.Status;

        inspection.Summary =
            Clean(request.Summary);

        inspection.CorrectiveActionRequired =
            Clean(request.CorrectiveActionRequired);

        inspection.CorrectiveActionDueDate =
            request.CorrectiveActionDueDate;

        inspection.SignedOffBy =
            request.SignedOffBy;

        inspection.SignedOffAtUtc =
            request.SignedOffAtUtc;

        var requestedItems = request.ChecklistItems
        .OrderBy(x => x.Sequence)
        .ToList();

    var existingItems = inspection.ChecklistItems
        .OrderBy(x => x.Sequence)
        .ToList();

    var existingBySequence = existingItems
        .ToDictionary(x => x.Sequence);

    var requestedSequences = requestedItems
        .Select(x => x.Sequence)
        .ToHashSet();

    foreach (var existingItem in existingItems)
    {
        if (!requestedSequences.Contains(existingItem.Sequence))
        {
            existingItem.IsDeleted = true;
            existingItem.DeletedAtUtc = DateTime.UtcNow;
            existingItem.UpdatedAtUtc = DateTime.UtcNow;
        }
    }

    foreach (var item in requestedItems)
    {
        if (existingBySequence.TryGetValue(
            item.Sequence,
            out var existingItem))
        {
            existingItem.Requirement =
                item.Requirement.Trim();

            existingItem.IsCompliant =
                item.IsCompliant;

            existingItem.Observation =
                Clean(item.Observation);

            existingItem.CorrectiveAction =
                Clean(item.CorrectiveAction);

            existingItem.DueDate =
                item.DueDate;

            existingItem.IsResolved =
                item.IsResolved;

            existingItem.IsDeleted = false;
            existingItem.DeletedAtUtc = null;
            existingItem.UpdatedAtUtc = DateTime.UtcNow;
        }
        else
        {
            var newItem = new InspectionChecklistItem
            {
                CompanyId = inspection.CompanyId,
                InspectionId = inspection.Id,
                Sequence = item.Sequence,
                Requirement = item.Requirement.Trim(),
                IsCompliant = item.IsCompliant,
                Observation = Clean(item.Observation),
                CorrectiveAction =
                    Clean(item.CorrectiveAction),
                DueDate = item.DueDate,
                IsResolved = item.IsResolved
            };

            _dbContext.InspectionChecklistItems.Add(newItem);
            inspection.ChecklistItems.Add(newItem);
        }
    }

    await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(inspection);
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
            ?? throw new InspectionManagementException(
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

        throw new InspectionManagementException(
            "You are not authorized to manage inspections.");
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
            throw new InspectionManagementException(
                "You are not authorized to access this inspection.");
        }
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
            throw new InspectionManagementException(
                "The specified construction site was not found in the company.");
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
            throw new InspectionManagementException(
                "The specified project was not found in the company.");
        }
    }

    private async Task EnsureInspectorEmployeeAccess(
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
            throw new InspectionManagementException(
                "The specified inspector employee was not found in the company.");
        }
    }

    private static Guid GetActorCompanyId(
        User actor)
    {
        if (!actor.CompanyId.HasValue)
        {
            throw new InspectionManagementException(
                "Current user is not assigned to a company.");
        }

        return actor.CompanyId.Value;
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

    private static void ValidateInspection(
        string inspectionNumber,
        string inspectionType,
        string title,
        DateTime scheduledAtUtc,
        DateTime? startedAtUtc,
        DateTime? completedAtUtc,
        DateOnly? correctiveActionDueDate,
        DateTime? signedOffAtUtc,
        Domain.Enums.InspectionStatus status,
        Inspection? existingInspection)
    {
        if (string.IsNullOrWhiteSpace(inspectionNumber))
        {
            throw new InspectionManagementException(
                "Inspection number is required.");
        }

        if (inspectionNumber.Trim().Length > 50)
        {
            throw new InspectionManagementException(
                "Inspection number cannot exceed 50 characters.");
        }

        if (string.IsNullOrWhiteSpace(inspectionType))
        {
            throw new InspectionManagementException(
                "Inspection type is required.");
        }

        if (inspectionType.Trim().Length > 100)
        {
            throw new InspectionManagementException(
                "Inspection type cannot exceed 100 characters.");
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new InspectionManagementException(
                "Inspection title is required.");
        }

        if (title.Trim().Length > 200)
        {
            throw new InspectionManagementException(
                "Inspection title cannot exceed 200 characters.");
        }

        if (scheduledAtUtc == default)
        {
            throw new InspectionManagementException(
                "Scheduled inspection date and time is required.");
        }

        if (startedAtUtc.HasValue &&
            startedAtUtc.Value < scheduledAtUtc)
        {
            throw new InspectionManagementException(
                "Inspection start time cannot be earlier than the scheduled time.");
        }

        if (completedAtUtc.HasValue &&
            !startedAtUtc.HasValue)
        {
            throw new InspectionManagementException(
                "An inspection cannot be completed before it has started.");
        }

        if (completedAtUtc.HasValue &&
            startedAtUtc.HasValue &&
            completedAtUtc.Value < startedAtUtc.Value)
        {
            throw new InspectionManagementException(
                "Inspection completion time cannot be earlier than the start time.");
        }

        if (correctiveActionDueDate.HasValue &&
            status != Domain.Enums.InspectionStatus.CorrectiveActionRequired &&
            status != Domain.Enums.InspectionStatus.PassedWithObservations &&
            status != Domain.Enums.InspectionStatus.Failed)
        {
            throw new InspectionManagementException(
                "A corrective action due date requires an inspection status that supports corrective action.");
        }

        if (status == Domain.Enums.InspectionStatus.InProgress &&
            !startedAtUtc.HasValue)
        {
            throw new InspectionManagementException(
                "An inspection marked InProgress must have a start time.");
        }

        if ((status == Domain.Enums.InspectionStatus.Passed ||
             status == Domain.Enums.InspectionStatus.PassedWithObservations ||
             status == Domain.Enums.InspectionStatus.Failed ||
             status == Domain.Enums.InspectionStatus.Closed) &&
            !completedAtUtc.HasValue)
        {
            throw new InspectionManagementException(
                "A completed inspection status requires a completion time.");
        }

        if (status == Domain.Enums.InspectionStatus.Closed &&
    !signedOffAtUtc.HasValue)
{
    throw new InspectionManagementException(
        "A closed inspection must be signed off.");
}
    }

    private static void ValidateChecklistItems(
        IEnumerable<InspectionChecklistItemRequest> items)
    {
        var list = items.ToList();

        var duplicateSequence = list
            .GroupBy(x => x.Sequence)
            .Any(x => x.Count() > 1);

        if (duplicateSequence)
        {
            throw new InspectionManagementException(
                "Checklist item sequence numbers must be unique.");
        }

        foreach (var item in list)
        {
            if (item.Sequence <= 0)
            {
                throw new InspectionManagementException(
                    "Checklist item sequence must be greater than zero.");
            }

            if (string.IsNullOrWhiteSpace(item.Requirement))
            {
                throw new InspectionManagementException(
                    "Checklist item requirement is required.");
            }

            if (item.Requirement.Trim().Length > 500)
            {
                throw new InspectionManagementException(
                    "Checklist item requirement cannot exceed 500 characters.");
            }

            if (item.IsResolved &&
                item.IsCompliant != true)
            {
                throw new InspectionManagementException(
                    "A resolved checklist item must be compliant.");
            }
        }
    }

    private static string? Clean(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static InspectionResponse Map(
        Inspection inspection)
    {
        return new InspectionResponse
        {
            Id = inspection.Id,
            CompanyId = inspection.CompanyId,
            ConstructionSiteId =
                inspection.ConstructionSiteId,
            ConstructionSiteName =
                inspection.ConstructionSite?.Name ?? string.Empty,
            ProjectId = inspection.ProjectId,
            InspectionNumber =
                inspection.InspectionNumber,
            InspectionType =
                inspection.InspectionType,
            Title = inspection.Title,
            ScheduledAtUtc =
                inspection.ScheduledAtUtc,
            StartedAtUtc =
                inspection.StartedAtUtc,
            CompletedAtUtc =
                inspection.CompletedAtUtc,
            InspectorEmployeeId =
                inspection.InspectorEmployeeId,
            ExternalInspectorName =
                inspection.ExternalInspectorName,
            ExternalOrganization =
                inspection.ExternalOrganization,
            Status = inspection.Status,
            Summary =
                inspection.Summary,
            CorrectiveActionRequired =
                inspection.CorrectiveActionRequired,
            CorrectiveActionDueDate =
                inspection.CorrectiveActionDueDate,
            SignedOffBy =
                inspection.SignedOffBy,
            SignedOffAtUtc =
                inspection.SignedOffAtUtc,
            ChecklistItems =
                inspection.ChecklistItems
                    .Where(x => !x.IsDeleted)
                    .OrderBy(x => x.Sequence)
                    .Select(x =>
                        new InspectionChecklistItemResponse
                        {
                            Id = x.Id,
                            Sequence = x.Sequence,
                            Requirement = x.Requirement,
                            IsCompliant = x.IsCompliant,
                            Observation = x.Observation,
                            CorrectiveAction =
                                x.CorrectiveAction,
                            DueDate = x.DueDate,
                            IsResolved = x.IsResolved
                        })
                    .ToList(),
            CreatedAtUtc =
                inspection.CreatedAtUtc,
            UpdatedAtUtc =
                inspection.UpdatedAtUtc
        };
    }
}














