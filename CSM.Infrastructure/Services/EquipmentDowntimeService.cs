using CSM.Application.Common.Exceptions;
using CSM.Application.Common.Security;
using CSM.Application.Procurement.EquipmentDowntime;
using CSM.Application.Procurement.EquipmentDowntime.Dtos;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Entities.Procurement;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public sealed class EquipmentDowntimeService
    : IEquipmentDowntimeService
{
    private readonly ApplicationDbContext _dbContext;

    public EquipmentDowntimeService(
        ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<EquipmentDowntimeResponse>> GetAllAsync(
        Guid currentUserId,
        Guid? equipmentId = null,
        Guid? constructionSiteId = null,
        bool? openOnly = null,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanManage(actor);

        var query = _dbContext.EquipmentDowntimeRecords
            .AsNoTracking()
            .Include(x => x.Equipment)
            .Include(x => x.ConstructionSite)
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
                x => x.ConstructionSiteId ==
                     constructionSiteId.Value);
        }

        if (openOnly.HasValue)
        {
            query = openOnly.Value
                ? query.Where(x => !x.EndedAtUtc.HasValue)
                : query.Where(x => x.EndedAtUtc.HasValue);
        }

        var records = await query
            .OrderByDescending(x => x.StartedAtUtc)
            .ToListAsync(cancellationToken);

        return records
            .Select(Map)
            .ToArray();
    }

    public async Task<EquipmentDowntimeResponse> GetByIdAsync(
        Guid currentUserId,
        Guid downtimeId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanManage(actor);

        var downtime = await GetDowntimeAsync(
            downtimeId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            downtime.CompanyId);

        return Map(downtime);
    }

    public async Task<EquipmentDowntimeResponse> CreateAsync(
        Guid currentUserId,
        CreateEquipmentDowntimeRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanManage(actor);

        ValidateRequest(
            request.DowntimeNumber,
            request.Reason,
            request.StartedAtUtc,
            request.EndedAtUtc);

        var equipment = await _dbContext.Equipment
            .SingleOrDefaultAsync(
                x => x.Id == request.EquipmentId,
                cancellationToken)
            ?? throw new EquipmentDowntimeManagementException(
                "Equipment was not found.");

        EnsureCompanyAccess(
            actor,
            equipment.CompanyId);

        if (!equipment.IsActive)
        {
            throw new EquipmentDowntimeManagementException(
                "Inactive equipment cannot have downtime recorded.");
        }

        if (request.ConstructionSiteId.HasValue)
        {
            await EnsureConstructionSiteAccess(
                equipment.CompanyId,
                request.ConstructionSiteId.Value,
                cancellationToken);
        }

        var duplicateNumber = await _dbContext
            .EquipmentDowntimeRecords
            .AnyAsync(
                x =>
                    x.CompanyId == equipment.CompanyId &&
                    x.DowntimeNumber == request.DowntimeNumber.Trim(),
                cancellationToken);

        if (duplicateNumber)
        {
            throw new EquipmentDowntimeManagementException(
                "Downtime number already exists.");
        }

        if (!request.EndedAtUtc.HasValue)
        {
            var openDowntime = await _dbContext
                .EquipmentDowntimeRecords
                .AnyAsync(
                    x =>
                        x.CompanyId == equipment.CompanyId &&
                        x.EquipmentId == equipment.Id &&
                        !x.EndedAtUtc.HasValue,
                    cancellationToken);

            if (openDowntime)
            {
                throw new EquipmentDowntimeManagementException(
                    "This equipment already has an open downtime record.");
            }
        }

        var downtime = new EquipmentDowntime
        {
            CompanyId = equipment.CompanyId,
            EquipmentId = equipment.Id,
            ConstructionSiteId = request.ConstructionSiteId,
            StartedAtUtc = request.StartedAtUtc,
            EndedAtUtc = request.EndedAtUtc,
            DowntimeNumber = request.DowntimeNumber.Trim(),
            Reason = request.Reason.Trim(),
            Resolution = Clean(request.Resolution),
            Equipment = equipment
        };

        _dbContext.EquipmentDowntimeRecords.Add(downtime);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        await _dbContext.Entry(downtime)
            .Reference(x => x.ConstructionSite)
            .LoadAsync(cancellationToken);

        return Map(downtime);
    }

    public async Task<EquipmentDowntimeResponse> UpdateAsync(
        Guid currentUserId,
        Guid downtimeId,
        UpdateEquipmentDowntimeRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanManage(actor);

        ValidateRequest(
            "VALID",
            request.Reason,
            request.StartedAtUtc,
            request.EndedAtUtc,
            validateNumber: false);

        var downtime = await GetDowntimeAsync(
            downtimeId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            downtime.CompanyId);

        if (request.ConstructionSiteId.HasValue)
        {
            await EnsureConstructionSiteAccess(
                downtime.CompanyId,
                request.ConstructionSiteId.Value,
                cancellationToken);
        }

        if (request.EndedAtUtc.HasValue &&
            request.EndedAtUtc.Value < request.StartedAtUtc)
        {
            throw new EquipmentDowntimeManagementException(
                "Downtime end time cannot be earlier than start time.");
        }

        downtime.ConstructionSiteId =
            request.ConstructionSiteId;

        downtime.StartedAtUtc =
            request.StartedAtUtc;

        downtime.EndedAtUtc =
            request.EndedAtUtc;

        downtime.Reason =
            request.Reason.Trim();

        downtime.Resolution =
            Clean(request.Resolution);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(downtime);
    }

    public async Task<EquipmentDowntimeResponse> CloseAsync(
        Guid currentUserId,
        Guid downtimeId,
        DateTime endedAtUtc,
        string? resolution,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanManage(actor);

        var downtime = await GetDowntimeAsync(
            downtimeId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            downtime.CompanyId);

        if (downtime.EndedAtUtc.HasValue)
        {
            throw new EquipmentDowntimeManagementException(
                "This downtime record is already closed.");
        }

        if (endedAtUtc < downtime.StartedAtUtc)
        {
            throw new EquipmentDowntimeManagementException(
                "Downtime end time cannot be earlier than start time.");
        }

        downtime.EndedAtUtc = endedAtUtc;
        downtime.Resolution = Clean(resolution);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(downtime);
    }

    private async Task<EquipmentDowntime> GetDowntimeAsync(
        Guid downtimeId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.EquipmentDowntimeRecords
            .Include(x => x.Equipment)
            .Include(x => x.ConstructionSite)
            .SingleOrDefaultAsync(
                x => x.Id == downtimeId,
                cancellationToken)
            ?? throw new EquipmentDowntimeManagementException(
                "Equipment downtime record was not found.");
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
                    x.CompanyId == companyId,
                cancellationToken);

        if (!exists)
        {
            throw new EquipmentDowntimeManagementException(
                "Construction site was not found in the equipment company.");
        }
    }

    private static void ValidateRequest(
        string downtimeNumber,
        string reason,
        DateTime startedAtUtc,
        DateTime? endedAtUtc,
        bool validateNumber = true)
    {
        if (validateNumber)
        {
            if (string.IsNullOrWhiteSpace(downtimeNumber))
            {
                throw new EquipmentDowntimeManagementException(
                    "Downtime number is required.");
            }

            if (downtimeNumber.Trim().Length > 30)
            {
                throw new EquipmentDowntimeManagementException(
                    "Downtime number cannot exceed 30 characters.");
            }
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new EquipmentDowntimeManagementException(
                "Downtime reason is required.");
        }

        if (reason.Trim().Length > 500)
        {
            throw new EquipmentDowntimeManagementException(
                "Downtime reason cannot exceed 500 characters.");
        }

        if (endedAtUtc.HasValue &&
            endedAtUtc.Value < startedAtUtc)
        {
            throw new EquipmentDowntimeManagementException(
                "Downtime end time cannot be earlier than start time.");
        }
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
            ?? throw new EquipmentDowntimeManagementException(
                "Authenticated user was not found.");
    }

    private static void EnsureCanManage(User actor)
    {
        if (IsSuperAdmin(actor) ||
            HasRole(actor, AppRoles.CompanyAdmin) ||
            HasRole(actor, AppRoles.SiteManager))
        {
            return;
        }

        throw new EquipmentDowntimeManagementException(
            "You are not authorized to manage equipment downtime.");
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
            throw new EquipmentDowntimeManagementException(
                "You are not authorized to access this equipment downtime record.");
        }
    }

    private static Guid GetActorCompanyId(User actor)
    {
        if (!actor.CompanyId.HasValue)
        {
            throw new EquipmentDowntimeManagementException(
                "Current user is not assigned to a company.");
        }

        return actor.CompanyId.Value;
    }

    private static bool IsSuperAdmin(User actor)
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

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static EquipmentDowntimeResponse Map(
        EquipmentDowntime downtime)
    {
        double? durationHours = null;

        if (downtime.EndedAtUtc.HasValue)
        {
            durationHours =
                (downtime.EndedAtUtc.Value -
                 downtime.StartedAtUtc)
                .TotalHours;
        }

        return new EquipmentDowntimeResponse(
            downtime.Id,
            downtime.CompanyId,
            downtime.EquipmentId,
            downtime.Equipment.EquipmentCode,
            downtime.Equipment.Name,
            downtime.ConstructionSiteId,
            downtime.ConstructionSite?.SiteCode,
            downtime.ConstructionSite?.Name,
            downtime.StartedAtUtc,
            downtime.EndedAtUtc,
            downtime.DowntimeNumber,
            downtime.Reason,
            downtime.Resolution,
            durationHours,
            downtime.CreatedAtUtc,
            downtime.UpdatedAtUtc);
    }
}
