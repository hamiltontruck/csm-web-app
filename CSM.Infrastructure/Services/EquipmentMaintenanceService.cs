using CSM.Application.Common.Exceptions;
using CSM.Application.Procurement.EquipmentMaintenance;
using CSM.Application.Procurement.EquipmentMaintenance.Dtos;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Procurement;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public sealed class EquipmentMaintenanceService :
    IEquipmentMaintenanceService
{
    private readonly ApplicationDbContext _dbContext;

    public EquipmentMaintenanceService(
        ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<EquipmentMaintenanceResponse>> GetAllAsync(
        Guid currentUserId,
        Guid? equipmentId = null,
        bool? completedOnly = null,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanAccess(actor);

        var query = _dbContext.EquipmentMaintenanceRecords
            .AsNoTracking()
            .Include(x => x.Equipment)
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

        if (completedOnly == true)
        {
            query = query.Where(
                x => x.CompletedAtUtc != null);
        }
        else if (completedOnly == false)
        {
            query = query.Where(
                x => x.CompletedAtUtc == null);
        }

        var records = await query
            .OrderByDescending(x => x.ScheduledAtUtc)
            .ThenBy(x => x.MaintenanceType)
            .ToListAsync(cancellationToken);

        return records
            .Select(Map)
            .ToList();
    }

    public async Task<EquipmentMaintenanceResponse> GetByIdAsync(
        Guid currentUserId,
        Guid maintenanceId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanAccess(actor);

        var maintenance = await GetMaintenanceAsync(
            maintenanceId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            maintenance.CompanyId);

        return Map(maintenance);
    }

    public async Task<EquipmentMaintenanceResponse> CreateAsync(
        Guid currentUserId,
        CreateEquipmentMaintenanceRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanModify(actor);

        var companyId = GetActorCompanyId(actor);

        ValidateRequiredText(
            request.MaintenanceType,
            "Maintenance type");

        ValidateDate(
            request.ScheduledAtUtc,
            "Scheduled date");

        ValidateMeter(
            request.MeterReading,
            "Meter reading");

        ValidateMeter(
            request.NextMaintenanceMeterReading,
            "Next maintenance meter reading");

        ValidateCost(request.Cost);

        ValidateCurrency(request.CurrencyCode);

        var equipment = await _dbContext.Equipment
            .SingleOrDefaultAsync(
                x => x.Id == request.EquipmentId,
                cancellationToken)
            ?? throw new EquipmentMaintenanceManagementException(
                "Equipment was not found.");

        EnsureCompanyAccess(
            actor,
            equipment.CompanyId);

        if (equipment.CompanyId != companyId)
        {
            throw new EquipmentMaintenanceManagementException(
                "Equipment does not belong to the current company.");
        }

        if (!equipment.IsActive)
        {
            throw new EquipmentMaintenanceManagementException(
                "Inactive equipment cannot have maintenance records.");
        }

        if (equipment.Status == EquipmentStatus.Retired)
        {
            throw new EquipmentMaintenanceManagementException(
                "Retired equipment cannot have maintenance records.");
        }

        if (request.NextMaintenanceAtUtc.HasValue &&
            request.NextMaintenanceAtUtc.Value <
            request.ScheduledAtUtc)
        {
            throw new EquipmentMaintenanceManagementException(
                "Next maintenance date cannot be earlier than the scheduled date.");
        }

        var maintenance = new EquipmentMaintenance
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            EquipmentId = equipment.Id,
            MaintenanceType = request.MaintenanceType.Trim(),
            ScheduledAtUtc = EnsureUtc(request.ScheduledAtUtc),
            MeterReading = request.MeterReading,
            Cost = request.Cost,
            CurrencyCode = request.CurrencyCode.Trim().ToUpperInvariant(),
            ServiceProvider = Clean(request.ServiceProvider),
            Description = Clean(request.Description),
            PartsReplaced = Clean(request.PartsReplaced),
            NextMaintenanceAtUtc = request.NextMaintenanceAtUtc.HasValue
                ? EnsureUtc(request.NextMaintenanceAtUtc.Value)
                : null,
            NextMaintenanceMeterReading =
                request.NextMaintenanceMeterReading,
            CreatedBy = currentUserId
        };

        _dbContext.EquipmentMaintenanceRecords.Add(maintenance);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        maintenance.Equipment = equipment;

        return Map(maintenance);
    }

    public async Task<EquipmentMaintenanceResponse> UpdateAsync(
        Guid currentUserId,
        Guid maintenanceId,
        UpdateEquipmentMaintenanceRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanModify(actor);

        var maintenance = await GetMaintenanceAsync(
            maintenanceId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            maintenance.CompanyId);

        ValidateRequiredText(
            request.MaintenanceType,
            "Maintenance type");

        ValidateDate(
            request.ScheduledAtUtc,
            "Scheduled date");

        ValidateMeter(
            request.MeterReading,
            "Meter reading");

        ValidateMeter(
            request.NextMaintenanceMeterReading,
            "Next maintenance meter reading");

        ValidateCost(request.Cost);

        ValidateCurrency(request.CurrencyCode);

        var scheduledAtUtc =
            EnsureUtc(request.ScheduledAtUtc);

        var startedAtUtc =
            request.StartedAtUtc.HasValue
                ? EnsureUtc(request.StartedAtUtc.Value)
                : (DateTime?)null;

        var completedAtUtc =
            request.CompletedAtUtc.HasValue
                ? EnsureUtc(request.CompletedAtUtc.Value)
                : (DateTime?)null;

        if (startedAtUtc.HasValue &&
            startedAtUtc.Value < scheduledAtUtc)
        {
            throw new EquipmentMaintenanceManagementException(
                "Maintenance start date cannot be earlier than the scheduled date.");
        }

        if (completedAtUtc.HasValue)
        {
            if (!startedAtUtc.HasValue)
            {
                throw new EquipmentMaintenanceManagementException(
                    "Maintenance cannot be completed before it is started.");
            }

            if (completedAtUtc.Value < startedAtUtc.Value)
            {
                throw new EquipmentMaintenanceManagementException(
                    "Maintenance completion date cannot be earlier than the start date.");
            }
        }

        if (request.NextMaintenanceAtUtc.HasValue &&
            request.NextMaintenanceAtUtc.Value <
            scheduledAtUtc)
        {
            throw new EquipmentMaintenanceManagementException(
                "Next maintenance date cannot be earlier than the scheduled date.");
        }

        maintenance.MaintenanceType =
            request.MaintenanceType.Trim();

        maintenance.ScheduledAtUtc =
            scheduledAtUtc;

        maintenance.StartedAtUtc =
            startedAtUtc;

        maintenance.CompletedAtUtc =
            completedAtUtc;

        maintenance.MeterReading =
            request.MeterReading;

        maintenance.Cost =
            request.Cost;

        maintenance.CurrencyCode =
            request.CurrencyCode.Trim().ToUpperInvariant();

        maintenance.ServiceProvider =
            Clean(request.ServiceProvider);

        maintenance.Description =
            Clean(request.Description);

        maintenance.PartsReplaced =
            Clean(request.PartsReplaced);

        maintenance.NextMaintenanceAtUtc =
            request.NextMaintenanceAtUtc.HasValue
                ? EnsureUtc(request.NextMaintenanceAtUtc.Value)
                : null;

        maintenance.NextMaintenanceMeterReading =
            request.NextMaintenanceMeterReading;

        maintenance.UpdatedBy = currentUserId;

        if (startedAtUtc.HasValue &&
            !completedAtUtc.HasValue)
        {
            maintenance.Equipment.Status =
                EquipmentStatus.UnderMaintenance;

            maintenance.Equipment.UpdatedBy =
                currentUserId;
        }
        else if (completedAtUtc.HasValue)
        {
            maintenance.Equipment.Status =
                EquipmentStatus.Available;

            maintenance.Equipment.UpdatedBy =
                currentUserId;

            if (request.MeterReading.HasValue)
            {
                maintenance.Equipment.CurrentMeterReading =
                    request.MeterReading.Value;
            }
        }

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(maintenance);
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
            ?? throw new EquipmentMaintenanceManagementException(
                "Current user was not found or is inactive.");
    }

    private async Task<EquipmentMaintenance> GetMaintenanceAsync(
        Guid maintenanceId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.EquipmentMaintenanceRecords
            .Include(x => x.Equipment)
            .SingleOrDefaultAsync(
                x => x.Id == maintenanceId,
                cancellationToken)
            ?? throw new EquipmentMaintenanceManagementException(
                "Equipment maintenance record was not found.");
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

        throw new EquipmentMaintenanceManagementException(
            "You are not authorized to access equipment maintenance records.");
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
            throw new EquipmentMaintenanceManagementException(
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
            throw new EquipmentMaintenanceManagementException(
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

    private static void ValidateRequiredText(
        string? value,
        string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new EquipmentMaintenanceManagementException(
                $"{fieldName} is required.");
        }
    }

    private static void ValidateDate(
        DateTime value,
        string fieldName)
    {
        if (value == default)
        {
            throw new EquipmentMaintenanceManagementException(
                $"{fieldName} is required.");
        }
    }

    private static void ValidateMeter(
        decimal? value,
        string fieldName)
    {
        if (value.HasValue &&
            value.Value < 0m)
        {
            throw new EquipmentMaintenanceManagementException(
                $"{fieldName} cannot be negative.");
        }
    }

    private static void ValidateCost(
        decimal? value)
    {
        if (value.HasValue &&
            value.Value < 0m)
        {
            throw new EquipmentMaintenanceManagementException(
                "Maintenance cost cannot be negative.");
        }
    }

    private static void ValidateCurrency(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Trim().Length != 3)
        {
            throw new EquipmentMaintenanceManagementException(
                "Currency code must contain exactly 3 characters.");
        }
    }

    private static DateTime EnsureUtc(
        DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(
                value,
                DateTimeKind.Utc)
        };
    }

    private static string? Clean(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static EquipmentMaintenanceResponse Map(
        EquipmentMaintenance maintenance)
    {
        return new EquipmentMaintenanceResponse(
            maintenance.Id,
            maintenance.CompanyId,
            maintenance.EquipmentId,
            maintenance.Equipment.EquipmentCode,
            maintenance.Equipment.Name,
            maintenance.MaintenanceType,
            maintenance.ScheduledAtUtc,
            maintenance.StartedAtUtc,
            maintenance.CompletedAtUtc,
            maintenance.MeterReading,
            maintenance.Cost,
            maintenance.CurrencyCode,
            maintenance.ServiceProvider,
            maintenance.Description,
            maintenance.PartsReplaced,
            maintenance.NextMaintenanceAtUtc,
            maintenance.NextMaintenanceMeterReading,
            maintenance.CreatedAtUtc,
            maintenance.UpdatedAtUtc);
    }
}
