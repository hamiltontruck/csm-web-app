using CSM.Application.Common.Exceptions;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Enums;
using CSM.Domain.Security;
using CSM.Application.Procurement.EquipmentHourLogs;
using CSM.Domain.Entities.Equipment;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public class EquipmentHourLogService
{
    private readonly ApplicationDbContext _dbContext;

    public EquipmentHourLogService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<EquipmentHourLogResponse>> GetForUserAsync(
        Guid userId, Guid? equipmentId, CancellationToken cancellationToken = default)
    {
        var companyId = await ResolveCompanyScopeAsync(userId, false, cancellationToken);
        return await QueryAsync(companyId, equipmentId, cancellationToken);
    }

    public async Task<EquipmentHourLogResponse> CreateForUserAsync(
        Guid userId, CreateEquipmentHourLogRequest request, CancellationToken cancellationToken = default)
    {
        var companyId = await ResolveCompanyScopeAsync(userId, true, cancellationToken);
        var equipment = await _dbContext.Equipment.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == request.EquipmentId, cancellationToken)
            ?? throw new EquipmentHourLogValidationException("Equipment was not found.");
        return await CreateAsync(companyId ?? equipment.CompanyId, request, cancellationToken);
    }

    private async Task<Guid?> ResolveCompanyScopeAsync(Guid userId, bool write, CancellationToken cancellationToken)
    {
        var actor = await _dbContext.Users.AsNoTracking()
            .Include(x => x.UserRoles).ThenInclude(x => x.Role).ThenInclude(x => x.GranularPermissions)
            .SingleOrDefaultAsync(x => x.Id == userId && x.IsActive, cancellationToken)
            ?? throw new EquipmentManagementException("Current user was not found or is inactive.");
        var roles = actor.UserRoles.Where(x => !x.IsDeleted && !x.Role.IsDeleted && x.Role.Name != "ProcurementOfficer")
            .Select(x => x.Role).ToArray();
        bool Grants(Role role, GranularPermission permission) => RolePermissionRules.Allows(role.Name, permission.ToString()) &&
            role.GranularPermissions.Any(x => x.Permission == permission);
        var view = GranularPermission.Procurement_Inventory_View;
        var adjust = GranularPermission.Procurement_Inventory_Adjust;
        if (!roles.Any(x => Grants(x, view)) || (write && !roles.Any(x => Grants(x, adjust))))
            throw new EquipmentManagementException("You do not have permission to access hour meter records.");
        // Only independently granted, live SuperAdmin authority grants global scope.
        if (roles.Any(x => x.Name == "SuperAdmin" && Grants(x, view) && (!write || Grants(x, adjust))))
            return null;
        if (actor.CompanyId is not Guid companyId || companyId == Guid.Empty)
            throw new EquipmentManagementException("Current user is not assigned to a company.");
        return companyId;
    }

    private async Task<List<EquipmentHourLogResponse>> QueryAsync(
        Guid? companyId,
        Guid? equipmentId,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.EquipmentHourLogs
            .AsNoTracking()
            .Include(x => x.Equipment)
            .Where(x => x.CompanyId == x.Equipment.CompanyId);

        if (companyId.HasValue)
            query = query.Where(x => x.CompanyId == companyId.Value);

        if (equipmentId.HasValue)
        {
            query = query.Where(x => x.EquipmentId == equipmentId.Value);
        }

        return await query
            .OrderByDescending(x => x.LoggedAtUtc)
            .Select(x => new EquipmentHourLogResponse(
                x.Id,
                x.EquipmentId,
                x.Equipment.EquipmentCode,
                x.Equipment.Name,

                x.LoggedAtUtc,

                x.PreviousMeterReading,
                x.CurrentMeterReading,
                x.HoursWorked,

                x.OperatorEmployeeId,
                x.OperatorName,

                x.ProjectId,
                x.ProjectName,

                x.Activity,
                x.Remarks
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task<EquipmentHourLogResponse> CreateAsync(
        Guid companyId,
        CreateEquipmentHourLogRequest request,
        CancellationToken cancellationToken = default)
    {
        var equipment = await _dbContext.Equipment
            .FirstOrDefaultAsync(
                x => x.Id == request.EquipmentId,
                cancellationToken);

        if (equipment == null)
        {
            throw new EquipmentHourLogValidationException(
                "Equipment was not found.");
        }
        if (equipment.CompanyId != companyId)
            throw new EquipmentManagementException("You cannot access equipment outside your company.");

        var site = await _dbContext.ConstructionSites
            .SingleOrDefaultAsync(
                x => x.Id == request.SiteId &&
                     x.CompanyId == equipment.CompanyId,
                cancellationToken);

        if (site == null)
        {
            throw new EquipmentHourLogValidationException("Construction site was not found.");
        }

        var hoursWorked = request.CurrentMeter - request.PreviousMeter;

        if (hoursWorked < 0)
        {
            throw new EquipmentHourLogValidationException(
                "Current meter cannot be less than previous meter.");
        }

        var entity = new EquipmentHourLog
        {
            CompanyId = companyId,
            EquipmentId = equipment.Id,

            ProjectId = site.Id,
            ProjectName = site.Name,
            LoggedAtUtc = EnsureUtc(request.LogDateUtc),

            OperatorName = request.OperatorName,
            Activity = request.Activity,

            PreviousMeterReading = request.PreviousMeter,
            CurrentMeterReading = request.CurrentMeter,
            HoursWorked = hoursWorked,

            Remarks = request.Remarks
        };

        _dbContext.EquipmentHourLogs.Add(entity);

        // Update current equipment meter
        equipment.CurrentMeterReading = request.CurrentMeter;

        await _dbContext.SaveChangesAsync(cancellationToken);

        entity.Equipment = equipment;

        return Map(entity);
    }

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static EquipmentHourLogResponse Map(EquipmentHourLog log)
    {
        return new EquipmentHourLogResponse(
            log.Id,
            log.EquipmentId,
            log.Equipment.EquipmentCode,
            log.Equipment.Name,

            log.LoggedAtUtc,

            log.PreviousMeterReading,
            log.CurrentMeterReading,
            log.HoursWorked,

            log.OperatorEmployeeId,
            log.OperatorName,

            log.ProjectId,
            log.ProjectName,

            log.Activity,
            log.Remarks
        );
    }
}
