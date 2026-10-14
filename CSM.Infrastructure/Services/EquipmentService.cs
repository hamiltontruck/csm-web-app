using CSM.Application.Common.Exceptions;
using CSM.Application.Procurement.Equipment;
using CSM.Application.Procurement.Equipment.Dtos;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Finance;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Procurement;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public sealed class EquipmentService : IEquipmentService
{
    private readonly ApplicationDbContext _dbContext;

    public EquipmentService(
        ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<EquipmentResponse>> GetAllAsync(
        Guid currentUserId,
        bool? isActive = null,
        EquipmentStatus? status = null,
        EquipmentOwnershipType? ownershipType = null,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanAccess(actor);

        var query = _dbContext.Equipment
            .AsNoTracking()
            .Include(x => x.Vendor)
            .AsQueryable();

        if (!IsSuperAdmin(actor))
        {
            query = query.Where(
                x => x.CompanyId == GetActorCompanyId(actor));
        }

        if (isActive.HasValue)
        {
            query = query.Where(
                x => x.IsActive == isActive.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(
                x => x.Status == status.Value);
        }

        if (ownershipType.HasValue)
        {
            query = query.Where(
                x => x.OwnershipType == ownershipType.Value);
        }

        var equipment = await query
            .OrderBy(x => x.EquipmentCode)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return equipment
            .Select(Map)
            .ToList();
    }

    public async Task<EquipmentResponse> GetByIdAsync(
        Guid currentUserId,
        Guid equipmentId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanAccess(actor);

        var equipment = await GetEquipmentAsync(
            equipmentId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            equipment.CompanyId);

        return Map(equipment);
    }

    public async Task<EquipmentResponse> CreateAsync(
        Guid currentUserId,
        CreateEquipmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanAccess(actor);

        var companyId = GetActorCompanyId(actor);

        var equipmentCode = Clean(request.EquipmentCode);
        var name = Clean(request.Name);
        var category = Clean(request.Category);
        var make = Clean(request.Make);
        var model = Clean(request.Model);
        var serialNumber = Clean(request.SerialNumber);
        var registrationNumber = Clean(request.RegistrationNumber);
        var rentalRateUnit = Clean(request.RentalRateUnit);
        var meterUnit = Clean(request.MeterUnit);

        ValidateRequiredFields(
            equipmentCode,
            name,
            meterUnit);

        ValidateLengths(
            equipmentCode!,
            name!,
            category,
            make,
            model,
            serialNumber,
            registrationNumber,
            rentalRateUnit,
            meterUnit!);

        ValidateNumericValues(
            request.PurchaseCost,
            request.RentalRate,
            request.CurrentMeterReading);

        ValidateRentalDates(
            request.RentalStartDate,
            request.RentalEndDate);

        await EnsureUniqueCodeAsync(
            companyId,
            equipmentCode!,
            null,
            cancellationToken);

        Vendor? vendor = null;

        if (request.VendorId.HasValue)
        {
            vendor = await GetVendorAsync(
                request.VendorId.Value,
                cancellationToken);

            EnsureVendorCompany(
                companyId,
                vendor);
        }

        var equipment = new Equipment
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            EquipmentCode = equipmentCode!,
            Name = name!,
            Category = category,
            Make = make,
            Model = model,
            SerialNumber = serialNumber,
            RegistrationNumber = registrationNumber,
            OwnershipType = request.OwnershipType,
            Status = request.Status,
            VendorId = vendor?.Id,
            PurchaseCost = request.PurchaseCost,
            RentalRate = request.RentalRate,
            RentalRateUnit = rentalRateUnit,
            RentalStartDate = request.RentalStartDate,
            RentalEndDate = request.RentalEndDate,
            CurrentMeterReading = request.CurrentMeterReading,
            MeterUnit = meterUnit!,
            IsActive = request.IsActive,
            CreatedBy = currentUserId
        };

        _dbContext.Equipment.Add(equipment);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        equipment.Vendor = vendor;

        return Map(equipment);
    }

    public async Task<EquipmentResponse> UpdateAsync(
        Guid currentUserId,
        Guid equipmentId,
        UpdateEquipmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanAccess(actor);

        var equipment = await GetEquipmentAsync(
            equipmentId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            equipment.CompanyId);

        var equipmentCode = Clean(request.EquipmentCode);
        var name = Clean(request.Name);
        var category = Clean(request.Category);
        var make = Clean(request.Make);
        var model = Clean(request.Model);
        var serialNumber = Clean(request.SerialNumber);
        var registrationNumber = Clean(request.RegistrationNumber);
        var rentalRateUnit = Clean(request.RentalRateUnit);
        var meterUnit = Clean(request.MeterUnit);

        ValidateRequiredFields(
            equipmentCode,
            name,
            meterUnit);

        ValidateLengths(
            equipmentCode!,
            name!,
            category,
            make,
            model,
            serialNumber,
            registrationNumber,
            rentalRateUnit,
            meterUnit!);

        ValidateNumericValues(
            request.PurchaseCost,
            request.RentalRate,
            request.CurrentMeterReading);

        ValidateRentalDates(
            request.RentalStartDate,
            request.RentalEndDate);

        await EnsureUniqueCodeAsync(
            equipment.CompanyId,
            equipmentCode!,
            equipment.Id,
            cancellationToken);

        Vendor? vendor = null;

        if (request.VendorId.HasValue)
        {
            vendor = await GetVendorAsync(
                request.VendorId.Value,
                cancellationToken);

            EnsureVendorCompany(
                equipment.CompanyId,
                vendor);
        }

        equipment.EquipmentCode = equipmentCode!;
        equipment.Name = name!;
        equipment.Category = category;
        equipment.Make = make;
        equipment.Model = model;
        equipment.SerialNumber = serialNumber;
        equipment.RegistrationNumber = registrationNumber;
        equipment.OwnershipType = request.OwnershipType;
        equipment.Status = request.Status;
        equipment.VendorId = vendor?.Id;
        equipment.PurchaseCost = request.PurchaseCost;
        equipment.RentalRate = request.RentalRate;
        equipment.RentalRateUnit = rentalRateUnit;
        equipment.RentalStartDate = request.RentalStartDate;
        equipment.RentalEndDate = request.RentalEndDate;
        equipment.CurrentMeterReading = request.CurrentMeterReading;
        equipment.MeterUnit = meterUnit!;
        equipment.IsActive = request.IsActive;
        equipment.UpdatedBy = currentUserId;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        equipment.Vendor = vendor;

        return Map(equipment);
    }

    private async Task<User> GetActorAsync(
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Users
            .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(
                x => x.Id == currentUserId &&
                     x.IsActive,
                cancellationToken)
            ?? throw new EquipmentManagementException(
                "Current user was not found or is inactive.");
    }

    private async Task<Equipment> GetEquipmentAsync(
        Guid equipmentId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Equipment
            .Include(x => x.Vendor)
            .SingleOrDefaultAsync(
                x => x.Id == equipmentId,
                cancellationToken)
            ?? throw new EquipmentManagementException(
                "Equipment was not found.");
    }

    private async Task<Vendor> GetVendorAsync(
        Guid vendorId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Vendors
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == vendorId,
                cancellationToken)
            ?? throw new EquipmentManagementException(
                "Vendor was not found.");
    }

    private async Task EnsureUniqueCodeAsync(
        Guid companyId,
        string equipmentCode,
        Guid? excludedEquipmentId,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Equipment
            .Where(
                x =>
                    x.CompanyId == companyId &&
                    x.EquipmentCode == equipmentCode);

        if (excludedEquipmentId.HasValue)
        {
            query = query.Where(
                x => x.Id != excludedEquipmentId.Value);
        }

        if (await query.AnyAsync(cancellationToken))
        {
            throw new EquipmentManagementException(
                "Equipment with this code already exists.");
        }
    }

    private static void EnsureVendorCompany(
        Guid companyId,
        Vendor vendor)
    {
        if (vendor.CompanyId != companyId)
        {
            throw new EquipmentManagementException(
                "Vendor must belong to the same company as the equipment.");
        }

        if (!vendor.IsActive)
        {
            throw new EquipmentManagementException(
                "Vendor must be active.");
        }
    }

    private static void ValidateRequiredFields(
        string? equipmentCode,
        string? name,
        string? meterUnit)
    {
        if (equipmentCode is null)
        {
            throw new EquipmentManagementException(
                "Equipment code is required.");
        }

        if (name is null)
        {
            throw new EquipmentManagementException(
                "Equipment name is required.");
        }

        if (meterUnit is null)
        {
            throw new EquipmentManagementException(
                "Meter unit is required.");
        }
    }

    private static void ValidateLengths(
        string equipmentCode,
        string name,
        string? category,
        string? make,
        string? model,
        string? serialNumber,
        string? registrationNumber,
        string? rentalRateUnit,
        string meterUnit)
    {
        if (equipmentCode.Length > 30)
        {
            throw new EquipmentManagementException(
                "Equipment code cannot exceed 30 characters.");
        }

        if (name.Length > 200)
        {
            throw new EquipmentManagementException(
                "Equipment name cannot exceed 200 characters.");
        }

        ValidateOptionalLength(
            category,
            100,
            "Equipment category");

        ValidateOptionalLength(
            make,
            100,
            "Equipment make");

        ValidateOptionalLength(
            model,
            100,
            "Equipment model");

        ValidateOptionalLength(
            serialNumber,
            100,
            "Equipment serial number");

        ValidateOptionalLength(
            registrationNumber,
            100,
            "Equipment registration number");

        ValidateOptionalLength(
            rentalRateUnit,
            30,
            "Rental rate unit");

        if (meterUnit.Length > 30)
        {
            throw new EquipmentManagementException(
                "Meter unit cannot exceed 30 characters.");
        }
    }

    private static void ValidateOptionalLength(
        string? value,
        int maxLength,
        string fieldName)
    {
        if (value is not null &&
            value.Length > maxLength)
        {
            throw new EquipmentManagementException(
                $"{fieldName} cannot exceed {maxLength} characters.");
        }
    }

    private static void ValidateNumericValues(
        decimal? purchaseCost,
        decimal? rentalRate,
        decimal currentMeterReading)
    {
        if (purchaseCost.HasValue &&
            purchaseCost.Value < 0m)
        {
            throw new EquipmentManagementException(
                "Purchase cost cannot be negative.");
        }

        if (rentalRate.HasValue &&
            rentalRate.Value < 0m)
        {
            throw new EquipmentManagementException(
                "Rental rate cannot be negative.");
        }

        if (currentMeterReading < 0m)
        {
            throw new EquipmentManagementException(
                "Current meter reading cannot be negative.");
        }
    }

    private static void ValidateRentalDates(
        DateOnly? rentalStartDate,
        DateOnly? rentalEndDate)
    {
        if (rentalStartDate.HasValue &&
            rentalEndDate.HasValue &&
            rentalEndDate.Value < rentalStartDate.Value)
        {
            throw new EquipmentManagementException(
                "Rental end date cannot be earlier than rental start date.");
        }
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

        throw new EquipmentManagementException(
            "You are not authorized to manage equipment.");
    }

    private static Guid GetActorCompanyId(
        User actor)
    {
        if (!actor.CompanyId.HasValue)
        {
            throw new EquipmentManagementException(
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
            throw new EquipmentManagementException(
                "You cannot access equipment outside your company.");
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

    private static EquipmentResponse Map(
        Equipment equipment)
    {
        return new EquipmentResponse
        {
            Id = equipment.Id,
            CompanyId = equipment.CompanyId,
            EquipmentCode = equipment.EquipmentCode,
            Name = equipment.Name,
            Category = equipment.Category,
            Make = equipment.Make,
            Model = equipment.Model,
            SerialNumber = equipment.SerialNumber,
            RegistrationNumber = equipment.RegistrationNumber,
            OwnershipType = equipment.OwnershipType,
            Status = equipment.Status,
            VendorId = equipment.VendorId,
            PurchaseCost = equipment.PurchaseCost,
            RentalRate = equipment.RentalRate,
            RentalRateUnit = equipment.RentalRateUnit,
            RentalStartDate = equipment.RentalStartDate,
            RentalEndDate = equipment.RentalEndDate,
            CurrentMeterReading = equipment.CurrentMeterReading,
            MeterUnit = equipment.MeterUnit,
            IsActive = equipment.IsActive,
            CreatedAtUtc = equipment.CreatedAtUtc,
            UpdatedAtUtc = equipment.UpdatedAtUtc
        };
    }
}
