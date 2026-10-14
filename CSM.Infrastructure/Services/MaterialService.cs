using CSM.Application.Common.Exceptions;
using CSM.Application.Procurement.Materials;
using CSM.Application.Procurement.Materials.Dtos;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Finance;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Procurement;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using CSM.Domain.Enums;

namespace CSM.Infrastructure.Services;

public sealed class MaterialService : IMaterialService
{
    private readonly ApplicationDbContext _dbContext;

    public MaterialService(
        ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<MaterialResponse>> GetAllAsync(
        Guid currentUserId,
        bool? isActive = null,
        string? category = null,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanAccess(actor);

        var query = _dbContext.Materials
            .AsNoTracking()
            .Include(x => x.DefaultCostCode)
            .Include(x => x.RebarSpec)
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

        var normalizedCategory = Clean(category);

        if (normalizedCategory is not null)
        {
            query = query.Where(
                x => x.Category == normalizedCategory);
        }

        var materials = await query
            .OrderBy(x => x.MaterialCode)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return materials
            .Select(Map)
            .ToList();
    }

    public async Task<MaterialResponse> GetByIdAsync(
        Guid currentUserId,
        Guid materialId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanAccess(actor);

        var material = await GetMaterialAsync(
            materialId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            material.CompanyId);

        return Map(material);
    }

    public async Task<MaterialResponse> CreateAsync(
        Guid currentUserId,
        CreateMaterialRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanAccess(actor);

        var companyId = GetActorCompanyId(actor);

        var materialCode = Clean(request.MaterialCode);
        var name = Clean(request.Name);
        var category = Clean(request.Category);
        var unitOfMeasure = Clean(request.UnitOfMeasure);

        ValidateRequiredFields(
            materialCode,
            name,
            category,
            unitOfMeasure);

        ValidateLengths(
            materialCode!,
            name!,
            category!,
            unitOfMeasure!);

        ValidateStandardUnitCost(
            request.StandardUnitCost);

        await EnsureUniqueCodeAsync(
            companyId,
            materialCode!,
            null,
            cancellationToken);

        CostCode? costCode = null;

        if (request.DefaultCostCodeId.HasValue)
        {
            costCode = await GetCostCodeAsync(
                request.DefaultCostCodeId.Value,
                cancellationToken);

            EnsureCostCodeCompany(
                companyId,
                costCode);
        }

        var material = new Material
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            MaterialCode = materialCode!,
            Name = name!,
            Description = Clean(request.Description),
            Category = category!,
            UnitOfMeasure = unitOfMeasure!,
            MaterialType = request.MaterialType,
            DefaultCostCodeId = costCode?.Id,
            StandardUnitCost = request.StandardUnitCost,
            IsActive = request.IsActive,
            CreatedBy = currentUserId
        };

        _dbContext.Materials.Add(material);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        material.DefaultCostCode = costCode;

        return Map(material);
    }

    public async Task<MaterialResponse> UpdateAsync(
        Guid currentUserId,
        Guid materialId,
        UpdateMaterialRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanAccess(actor);

        var material = await GetMaterialAsync(
            materialId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            material.CompanyId);

        var materialCode = Clean(request.MaterialCode);
        var name = Clean(request.Name);
        var category = Clean(request.Category);
        var unitOfMeasure = Clean(request.UnitOfMeasure);

        ValidateRequiredFields(
            materialCode,
            name,
            category,
            unitOfMeasure);

        ValidateLengths(
            materialCode!,
            name!,
            category!,
            unitOfMeasure!);

        ValidateStandardUnitCost(
            request.StandardUnitCost);

        await EnsureUniqueCodeAsync(
            material.CompanyId,
            materialCode!,
            material.Id,
            cancellationToken);

        CostCode? costCode = null;

        if (request.DefaultCostCodeId.HasValue)
        {
            costCode = await GetCostCodeAsync(
                request.DefaultCostCodeId.Value,
                cancellationToken);

            EnsureCostCodeCompany(
                material.CompanyId,
                costCode);
        }

        material.MaterialCode = materialCode!;
        material.Name = name!;
        material.Description = Clean(request.Description);
        material.Category = category!;
        material.UnitOfMeasure = unitOfMeasure!;
        material.MaterialType = request.MaterialType;
        material.DefaultCostCodeId = costCode?.Id;
        material.StandardUnitCost = request.StandardUnitCost;
        material.IsActive = request.IsActive;
        material.UpdatedBy = currentUserId;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        material.DefaultCostCode = costCode;

        return Map(material);
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
            ?? throw new MaterialManagementException(
                "Current user was not found or is inactive.");
    }
    public Task<IReadOnlyList<MaterialLookupDto>> GetCategoriesAsync(
        CancellationToken cancellationToken = default)
    {
        var categories = new[]
        {
            new MaterialLookupDto("Cement", "Cement"),
            new MaterialLookupDto("Steel", "Steel"),
            new MaterialLookupDto("Concrete", "Concrete"),
            new MaterialLookupDto("Rebar", "Rebar"),
            new MaterialLookupDto("Aggregate", "Aggregate"),
            new MaterialLookupDto("Finishing", "Finishing"),
        };

        return Task.FromResult<IReadOnlyList<MaterialLookupDto>>(categories);
    }

    public Task<IReadOnlyList<MaterialLookupDto>> GetUnitsAsync(
        CancellationToken cancellationToken = default)
    {
        var units = new[]
        {
            new MaterialLookupDto("Bag", "Bag"),
            new MaterialLookupDto("Piece", "Piece"),
            new MaterialLookupDto("Kilogram", "Kilogram"),
            new MaterialLookupDto("Ton", "Ton"),
            new MaterialLookupDto("Meter", "Meter"),
            new MaterialLookupDto("Meter Cube", "Meter Cube"),
            new MaterialLookupDto("Liter", "Liter"),
            new MaterialLookupDto("Roll", "Roll"),
        };

        return Task.FromResult<IReadOnlyList<MaterialLookupDto>>(units);
    }

    public Task<IReadOnlyList<MaterialLookupDto>> GetMaterialTypesAsync(
        CancellationToken cancellationToken = default)
    {
        var types = Enum.GetValues<MaterialType>()
            .Select(x => new MaterialLookupDto(
                x.ToString(),
                x.ToString()))
            .ToArray();

        return Task.FromResult<IReadOnlyList<MaterialLookupDto>>(types);
    }

    private async Task<Material> GetMaterialAsync(
        Guid materialId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Materials
            .Include(x => x.DefaultCostCode)
            .Include(x => x.RebarSpec)
            .SingleOrDefaultAsync(
                x => x.Id == materialId,
                cancellationToken)
            ?? throw new MaterialManagementException(
                "Material was not found.");
    }

    private async Task<CostCode> GetCostCodeAsync(
        Guid costCodeId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.CostCodes
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == costCodeId,
                cancellationToken)
            ?? throw new MaterialManagementException(
                "Default cost code was not found.");
    }

    private async Task EnsureUniqueCodeAsync(
        Guid companyId,
        string materialCode,
        Guid? excludedMaterialId,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Materials
            .Where(
                x =>
                    x.CompanyId == companyId &&
                    x.MaterialCode == materialCode);

        if (excludedMaterialId.HasValue)
        {
            query = query.Where(
                x => x.Id != excludedMaterialId.Value);
        }

        if (await query.AnyAsync(cancellationToken))
        {
            throw new MaterialManagementException(
                "A material with this code already exists.");
        }
    }

    private static void EnsureCostCodeCompany(
        Guid companyId,
        CostCode costCode)
    {
        if (costCode.CompanyId != companyId)
        {
            throw new MaterialManagementException(
                "Default cost code must belong to the same company as the material.");
        }

        if (!costCode.IsActive)
        {
            throw new MaterialManagementException(
                "Default cost code must be active.");
        }
    }

    private static void ValidateRequiredFields(
        string? materialCode,
        string? name,
        string? category,
        string? unitOfMeasure)
    {
        if (materialCode is null)
        {
            throw new MaterialManagementException(
                "Material code is required.");
        }

        if (name is null)
        {
            throw new MaterialManagementException(
                "Material name is required.");
        }

        if (category is null)
        {
            throw new MaterialManagementException(
                "Material category is required.");
        }

        if (unitOfMeasure is null)
        {
            throw new MaterialManagementException(
                "Unit of measure is required.");
        }
    }

    private static void ValidateLengths(
        string materialCode,
        string name,
        string category,
        string unitOfMeasure)
    {
        if (materialCode.Length > 30)
        {
            throw new MaterialManagementException(
                "Material code cannot exceed 30 characters.");
        }

        if (name.Length > 200)
        {
            throw new MaterialManagementException(
                "Material name cannot exceed 200 characters.");
        }

        if (category.Length > 100)
        {
            throw new MaterialManagementException(
                "Material category cannot exceed 100 characters.");
        }

        if (unitOfMeasure.Length > 30)
        {
            throw new MaterialManagementException(
                "Unit of measure cannot exceed 30 characters.");
        }
    }

    private static void ValidateStandardUnitCost(
        decimal? standardUnitCost)
    {
        if (standardUnitCost.HasValue &&
            standardUnitCost.Value < 0m)
        {
            throw new MaterialManagementException(
                "Standard unit cost cannot be negative.");
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

        throw new MaterialManagementException(
            "You are not authorized to manage materials.");
    }

    private static Guid GetActorCompanyId(
        User actor)
    {
        if (!actor.CompanyId.HasValue)
        {
            throw new MaterialManagementException(
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
            throw new MaterialManagementException(
                "You cannot access materials outside your company.");
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

    private static MaterialResponse Map(
        Material material)
    {
        return new MaterialResponse
        {
            Id = material.Id,
            CompanyId = material.CompanyId,
            MaterialCode = material.MaterialCode,
            Name = material.Name,
            Description = material.Description,
            Category = material.Category,
            UnitOfMeasure = material.UnitOfMeasure,
            MaterialType = material.MaterialType,
            RebarDiameterMm = material.RebarSpec?.DiameterMm,
            RebarGrade = material.RebarSpec?.Grade,
            DefaultCostCodeId = material.DefaultCostCodeId,
            DefaultCostCode = material.DefaultCostCode?.Code,
            DefaultCostCodeName = material.DefaultCostCode?.Name,
            StandardUnitCost = material.StandardUnitCost,
            IsActive = material.IsActive,
            CreatedAtUtc = material.CreatedAtUtc,
            UpdatedAtUtc = material.UpdatedAtUtc
        };
    }
}






