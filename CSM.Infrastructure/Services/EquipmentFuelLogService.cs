using CSM.Application.Common.Exceptions;
using CSM.Application.Procurement.EquipmentFuelLogs;
using CSM.Domain.Entities.Equipment;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public class EquipmentFuelLogService
{
    private readonly ApplicationDbContext _dbContext;

    public EquipmentFuelLogService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<EquipmentFuelLogResponse>> GetAllAsync(
        Guid userId,
        Guid? equipmentId,
        CancellationToken cancellationToken = default)
    {
        var companyId = await _dbContext.Users
            .Where(x => x.Id == userId)
            .Select(x => x.CompanyId)
            .SingleAsync(cancellationToken);

        var query = _dbContext.EquipmentFuelLogs
            .AsNoTracking()
            .Include(x => x.Equipment)
            .Where(x => x.CompanyId == companyId);

        if (equipmentId.HasValue)
        {
            query = query.Where(x => x.EquipmentId == equipmentId.Value);
        }

        var logs = await query
            .OrderByDescending(x => x.RefueledAtUtc)
            .ToListAsync(cancellationToken);

        return logs
            .Select(x => Map(x))
            .ToList();
    }

    public async Task<EquipmentFuelLogResponse> CreateAsync(
        Guid userId,
        CreateEquipmentFuelLogRequest request,
        CancellationToken cancellationToken = default)
    {
        var companyId = await _dbContext.Users
            .Where(x => x.Id == userId)
            .Select(x => x.CompanyId)
            .SingleAsync(cancellationToken);

        if (companyId == null)
        {
            throw new InvalidOperationException("User is not assigned to a company.");
        }

        var equipment = await _dbContext.Equipment
            .SingleOrDefaultAsync(
                x => x.Id == request.EquipmentId &&
                     x.CompanyId == companyId.Value,
                cancellationToken);

        if (equipment == null)
        {
            throw new InvalidOperationException("Equipment was not found.");
        }

        var assignment = await _dbContext.EquipmentAssignments
            .AsNoTracking()
            .Include(x => x.ConstructionSite)
            .Where(x =>
                x.EquipmentId == equipment.Id &&
                x.ReleasedAtUtc == null)
            .OrderByDescending(x => x.AssignedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var entity = new EquipmentFuelLog
        {
            CompanyId = companyId.Value,
            EquipmentId = equipment.Id,

            SiteId = assignment?.ConstructionSiteId,
            SiteName = assignment?.ConstructionSite?.Name,

            RefueledAtUtc = request.RefueledAtUtc,
            Liters = request.Liters,
            UnitPrice = request.UnitPrice,
            TotalCost = request.Liters * request.UnitPrice,
            MeterReading = request.MeterReading,
            FuelType = request.FuelType,
            SupplierName = request.SupplierName,
            InvoiceNumber = request.InvoiceNumber,
            Remarks = request.Remarks
        };

        _dbContext.EquipmentFuelLogs.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        entity.Equipment = equipment;

        return Map(entity);
    }

    public async Task<EquipmentFuelLogResponse> UpdateAsync(
        Guid userId,
        Guid fuelLogId,
        UpdateEquipmentFuelLogRequest request,
        CancellationToken cancellationToken = default)
    {
        var companyId = await _dbContext.Users
            .Where(x => x.Id == userId)
            .Select(x => x.CompanyId)
            .SingleAsync(cancellationToken);

        var log = await _dbContext.EquipmentFuelLogs
            .Include(x => x.Equipment)
            .SingleOrDefaultAsync(
                x => x.Id == fuelLogId &&
                     x.CompanyId == companyId,
                cancellationToken);

        if (log == null)
            throw new InvalidOperationException("Fuel log was not found.");

        log.RefueledAtUtc = request.RefueledAtUtc;
        log.Liters = request.Liters;
        log.UnitPrice = request.UnitPrice;
        log.TotalCost = request.Liters * request.UnitPrice;
        log.MeterReading = request.MeterReading;
        log.FuelType = request.FuelType;
        log.SupplierName = request.SupplierName;
        log.InvoiceNumber = request.InvoiceNumber;
        log.Remarks = request.Remarks;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Map(log);
    }

    public async Task<EquipmentFuelLogResponse> GetByIdAsync(
        Guid userId,
        Guid fuelLogId,
        CancellationToken cancellationToken = default)
    {
        var companyId = await _dbContext.Users
            .Where(x => x.Id == userId)
            .Select(x => x.CompanyId)
            .SingleAsync(cancellationToken);

        var log = await _dbContext.EquipmentFuelLogs
            .AsNoTracking()
            .Include(x => x.Equipment)
            .SingleOrDefaultAsync(
                x => x.Id == fuelLogId &&
                     x.CompanyId == companyId,
                cancellationToken);

        if (log == null)
            throw new EquipmentManagementException("Fuel log was not found.");

        return Map(log);
    }

    private static EquipmentFuelLogResponse Map(EquipmentFuelLog log)
    {
        return new EquipmentFuelLogResponse(
            log.Id,
            log.EquipmentId,
            log.Equipment.EquipmentCode,
            log.Equipment.Name,
            log.SiteId,
            log.SiteName,
            log.RefueledAtUtc,
            log.Liters,
            log.UnitPrice,
            log.TotalCost,
            log.MeterReading,
            log.FuelType,
            log.SupplierName,
            log.InvoiceNumber,
            log.Remarks);
    }
}