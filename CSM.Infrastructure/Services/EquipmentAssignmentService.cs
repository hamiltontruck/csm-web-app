using CSM.Application.Common.Exceptions;
using CSM.Application.Procurement.EquipmentAssignments;
using CSM.Application.Procurement.EquipmentAssignments.Dtos;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Entities.Procurement;
using CSM.Domain.Entities.Projects;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using ProcurementEquipment = CSM.Domain.Entities.Procurement.Equipment;
using ProcurementEquipmentAssignment = CSM.Domain.Entities.Procurement.EquipmentAssignment;



namespace CSM.Infrastructure.Services;

public sealed class EquipmentAssignmentService :
    IEquipmentAssignmentService
{
    private readonly ApplicationDbContext _dbContext;

    public EquipmentAssignmentService(
        ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<EquipmentAssignmentResponse>> GetAllAsync(
        Guid currentUserId,
        Guid? equipmentId = null,
        Guid? constructionSiteId = null,
        Guid? projectId = null,
        bool? activeOnly = null,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanAccess(actor);

        var query = _dbContext.EquipmentAssignments
            .AsNoTracking()
            .Include(x => x.Equipment)
            .Include(x => x.ConstructionSite)
            .Include(x => x.Project)
            .AsQueryable();

        if (!IsSuperAdmin(actor))
        {
            query = query.Where(
                x => x.CompanyId == GetActorCompanyId(actor));
        }

        if (equipmentId.HasValue)
        {
            query = query.Where(
                x => x.EquipmentId == equipmentId.Value);
        }

        if (constructionSiteId.HasValue)
        {
            query = query.Where(
                x => x.ConstructionSiteId == constructionSiteId.Value);
        }

        if (projectId.HasValue)
        {
            query = query.Where(
                x => x.ProjectId == projectId.Value);
        }

        if (activeOnly == true)
        {
            query = query.Where(
                x => x.ReleasedAtUtc == null);
        }
        else if (activeOnly == false)
        {
            query = query.Where(
                x => x.ReleasedAtUtc != null);
        }

        var assignments = await query
            .OrderByDescending(x => x.AssignedAtUtc)
            .ThenBy(x => x.AssignmentNumber)
            .ToListAsync(cancellationToken);

        return assignments
            .Select(Map)
            .ToList();
    }

    public async Task<EquipmentAssignmentResponse> GetByIdAsync(
        Guid currentUserId,
        Guid assignmentId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanAccess(actor);

        var assignment = await GetAssignmentAsync(
            assignmentId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            assignment.CompanyId);

        return Map(assignment);
    }

    public async Task<EquipmentAssignmentResponse> CreateAsync(
        Guid currentUserId,
        CreateEquipmentAssignmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanModify(actor);

        var companyId = GetActorCompanyId(actor);

        if (request.AssignedAtUtc.Kind == DateTimeKind.Unspecified)
        {
            request = request with
            {
                AssignedAtUtc = DateTime.SpecifyKind(
                    request.AssignedAtUtc,
                    DateTimeKind.Utc)
            };
        }

        if (request.AssignedAtUtc == default)
        {
            throw new EquipmentAssignmentManagementException(
                "Assignment date is required.");
        }

        ValidateMeter(
            request.MeterReadingAtAssignment,
            "Assignment meter reading");

        var equipment = await _dbContext.Equipment
            .SingleOrDefaultAsync(
                x => x.Id == request.EquipmentId,
                cancellationToken)
            ?? throw new EquipmentAssignmentManagementException(
                "Equipment was not found.");

        EnsureCompanyAccess(
            actor,
            equipment.CompanyId);

        if (!equipment.IsActive)
        {
            throw new EquipmentAssignmentManagementException(
                "Inactive equipment cannot be assigned.");
        }

        if (equipment.Status == EquipmentStatus.Retired ||
            equipment.Status == EquipmentStatus.OutOfService)
        {
            throw new EquipmentAssignmentManagementException(
                "Equipment cannot be assigned because it is retired or out of service.");
        }

        var site = await _dbContext.ConstructionSites
            .SingleOrDefaultAsync(
                x => x.Id == request.ConstructionSiteId,
                cancellationToken)
            ?? throw new EquipmentAssignmentManagementException(
                "Construction site was not found.");

        EnsureCompanyAccess(
            actor,
            site.CompanyId);

        if (site.CompanyId != companyId)
        {
            throw new EquipmentAssignmentManagementException(
                "Equipment and construction site must belong to the same company.");
        }

        Project? project = null;

        if (request.ProjectId.HasValue)
        {
            project = await _dbContext.Projects
                .SingleOrDefaultAsync(
                    x => x.Id == request.ProjectId.Value,
                    cancellationToken)
                ?? throw new EquipmentAssignmentManagementException(
                    "Project was not found.");

            EnsureCompanyAccess(
                actor,
                project.CompanyId);

            if (project.CompanyId != companyId)
            {
                throw new EquipmentAssignmentManagementException(
                    "Project must belong to the same company.");
            }

            if (project.ConstructionSiteId != request.ConstructionSiteId)
            {
                throw new EquipmentAssignmentManagementException(
                    "Project must belong to the selected construction site.");
            }
        }

        var hasOverlap = await _dbContext.EquipmentAssignments
            .AnyAsync(
                x =>
                    x.EquipmentId == request.EquipmentId &&
                    x.CompanyId == companyId &&
                    x.AssignedAtUtc <= request.AssignedAtUtc &&
                    x.ReleasedAtUtc == null,
                cancellationToken);

        if (hasOverlap)
        {
            throw new EquipmentAssignmentManagementException(
                "Equipment already has an active assignment.");
        }

        var assignmentNumber = await GenerateAssignmentNumberAsync(
            companyId,
            cancellationToken);

        var assignment = new ProcurementEquipmentAssignment
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            EquipmentId = equipment.Id,
            ConstructionSiteId = site.Id,
            ProjectId = project?.Id,
            AssignedAtUtc = request.AssignedAtUtc,
            MeterReadingAtAssignment =
                request.MeterReadingAtAssignment,
            Remarks = Clean(request.Remarks),
            AssignmentNumber = assignmentNumber,
            CreatedBy = currentUserId
        };

        equipment.Status = EquipmentStatus.Assigned;
        equipment.CurrentMeterReading =
            request.MeterReadingAtAssignment
            ?? equipment.CurrentMeterReading;
        equipment.UpdatedBy = currentUserId;

        _dbContext.EquipmentAssignments.Add(assignment);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        assignment.Equipment = equipment;
        assignment.ConstructionSite = site;
        assignment.Project = project;

        return Map(assignment);
    }

    public async Task<EquipmentAssignmentResponse> UpdateAsync(
        Guid currentUserId,
        Guid assignmentId,
        UpdateEquipmentAssignmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanModify(actor);

        var assignment = await GetAssignmentAsync(
            assignmentId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            assignment.CompanyId);

        if (request.AssignedAtUtc == default)
        {
            throw new EquipmentAssignmentManagementException(
                "Assignment date is required.");
        }

        ValidateMeter(
            request.MeterReadingAtAssignment,
            "Assignment meter reading");

        ValidateMeter(
            request.MeterReadingAtRelease,
            "Release meter reading");

        if (request.ReleasedAtUtc.HasValue &&
            request.ReleasedAtUtc.Value < request.AssignedAtUtc)
        {
            throw new EquipmentAssignmentManagementException(
                "Release date cannot be earlier than assignment date.");
        }

        if (request.MeterReadingAtAssignment.HasValue &&
            request.MeterReadingAtRelease.HasValue &&
            request.MeterReadingAtRelease.Value <
            request.MeterReadingAtAssignment.Value)
        {
            throw new EquipmentAssignmentManagementException(
                "Release meter reading cannot be lower than assignment meter reading.");
        }

        if (!request.ReleasedAtUtc.HasValue)
        {
            var overlap = await _dbContext.EquipmentAssignments
                .AnyAsync(
                    x =>
                        x.Id != assignment.Id &&
                        x.EquipmentId == assignment.EquipmentId &&
                        x.CompanyId == assignment.CompanyId &&
                        x.ReleasedAtUtc == null,
                    cancellationToken);

            if (overlap)
            {
                throw new EquipmentAssignmentManagementException(
                    "Equipment already has another active assignment.");
            }
        }

        assignment.AssignedAtUtc =
            request.AssignedAtUtc;

        assignment.ReleasedAtUtc =
            request.ReleasedAtUtc;

        assignment.MeterReadingAtAssignment =
            request.MeterReadingAtAssignment;

        assignment.MeterReadingAtRelease =
            request.MeterReadingAtRelease;

        assignment.Remarks =
            Clean(request.Remarks);

        assignment.UpdatedBy = currentUserId;

        assignment.Equipment.CurrentMeterReading =
            request.MeterReadingAtRelease
            ?? request.MeterReadingAtAssignment
            ?? assignment.Equipment.CurrentMeterReading;

        assignment.Equipment.Status =
            request.ReleasedAtUtc.HasValue
                ? EquipmentStatus.Available
                : EquipmentStatus.Assigned;

        assignment.Equipment.UpdatedBy = currentUserId;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(assignment);
    }

    public async Task<EquipmentAssignmentResponse> ReleaseAsync(
        Guid currentUserId,
        Guid assignmentId,
        DateTime releasedAtUtc,
        decimal? meterReadingAtRelease,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanModify(actor);

        var assignment = await GetAssignmentAsync(
            assignmentId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            assignment.CompanyId);

        if (releasedAtUtc.Kind == DateTimeKind.Unspecified)
        {
            releasedAtUtc = DateTime.SpecifyKind(
                releasedAtUtc,
                DateTimeKind.Utc);
        }

        if (releasedAtUtc < assignment.AssignedAtUtc)
        {
            throw new EquipmentAssignmentManagementException(
                "Release date cannot be earlier than assignment date.");
        }

        if (assignment.ReleasedAtUtc.HasValue)
        {
            throw new EquipmentAssignmentManagementException(
                "Equipment assignment has already been released.");
        }

        ValidateMeter(
            meterReadingAtRelease,
            "Release meter reading");

        if (meterReadingAtRelease.HasValue &&
            assignment.MeterReadingAtAssignment.HasValue &&
            meterReadingAtRelease.Value <
            assignment.MeterReadingAtAssignment.Value)
        {
            throw new EquipmentAssignmentManagementException(
                "Release meter reading cannot be lower than assignment meter reading.");
        }

        assignment.ReleasedAtUtc = releasedAtUtc;
        assignment.MeterReadingAtRelease =
            meterReadingAtRelease;
        assignment.UpdatedBy = currentUserId;

        assignment.Equipment.CurrentMeterReading =
            meterReadingAtRelease
            ?? assignment.Equipment.CurrentMeterReading;

        assignment.Equipment.Status =
            EquipmentStatus.Available;

        assignment.Equipment.UpdatedBy = currentUserId;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(assignment);
    }

    private async Task<User> GetActorAsync(
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Users
            .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(
                x =>
                    x.Id == currentUserId &&
                    x.IsActive,
                cancellationToken)
            ?? throw new EquipmentAssignmentManagementException(
                "Current user was not found or is inactive.");
    }

   private async Task<ProcurementEquipmentAssignment> GetAssignmentAsync(
       Guid assignmentId,
       CancellationToken cancellationToken)
    {
        return await _dbContext.EquipmentAssignments
            .Include(x => x.Equipment)
            .Include(x => x.ConstructionSite)
            .Include(x => x.Project)
            .SingleOrDefaultAsync(
                x => x.Id == assignmentId,
                cancellationToken)
            ?? throw new EquipmentAssignmentManagementException(
                "Equipment assignment was not found.");
    }

    private async Task<string> GenerateAssignmentNumberAsync(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var existingNumbers = await _dbContext.EquipmentAssignments
            .Where(x => x.CompanyId == companyId)
            .Select(x => x.AssignmentNumber)
            .ToListAsync(cancellationToken);

        var maxNumber = existingNumbers
            .Select(x =>
            {
                if (!x.StartsWith("EA-",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return 0;
                }

                return int.TryParse(
                    x[3..],
                    out var number)
                    ? number
                    : 0;
            })
            .DefaultIfEmpty(0)
            .Max();

        return $"EA-{maxNumber + 1:000000}";
    }

    private static void EnsureCanAccess(
        User actor)
    {
        if (IsSuperAdmin(actor) ||
            HasRole(actor, AppRoles.CompanyAdmin) ||
            HasRole(actor, AppRoles.SiteManager))
        {
            return;
        }

        throw new EquipmentAssignmentManagementException(
            "You are not authorized to access equipment assignments.");
    }

    private static void EnsureCanModify(
        User actor)
    {
        EnsureCanAccess(actor);
    }

    private static Guid GetActorCompanyId(
        User actor)
    {
        if (!actor.CompanyId.HasValue)
        {
            throw new EquipmentAssignmentManagementException(
                "Current user is not assigned to a company.");
        }

        return actor.CompanyId.Value;
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
            throw new EquipmentAssignmentManagementException(
                "You cannot access records outside your company.");
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

    private static void ValidateMeter(
        decimal? value,
        string fieldName)
    {
        if (value.HasValue && value.Value < 0m)
        {
            throw new EquipmentAssignmentManagementException(
                $"{fieldName} cannot be negative.");
        }
    }

    private static string? Clean(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static EquipmentAssignmentResponse Map(
        ProcurementEquipmentAssignment assignment)
    {
        return new EquipmentAssignmentResponse(
            assignment.Id,
            assignment.CompanyId,
            assignment.AssignmentNumber,
            assignment.EquipmentId,
            assignment.Equipment.EquipmentCode,
            assignment.Equipment.Name,
            assignment.ConstructionSiteId,
            assignment.ConstructionSite.Name,
            assignment.ProjectId,
            assignment.Project?.Name,
            assignment.AssignedAtUtc,
            assignment.ReleasedAtUtc,
            assignment.MeterReadingAtAssignment,
            assignment.MeterReadingAtRelease,
            assignment.Remarks,
            assignment.CreatedAtUtc,
            assignment.UpdatedAtUtc);
    }
}
