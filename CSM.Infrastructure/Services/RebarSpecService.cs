using CSM.Application.Common.Exceptions;
using CSM.Application.Procurement.RebarSpecs;
using CSM.Application.Procurement.RebarSpecs.Dtos;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Procurement;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public sealed class RebarSpecService : IRebarSpecService
{
    private readonly ApplicationDbContext _dbContext;

    public RebarSpecService(
        ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<RebarSpecResponse> GetByMaterialIdAsync(
        Guid currentUserId,
        Guid materialId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanAccess(actor);

        var material = await GetMaterialAsync(
            actor,
            materialId,
            cancellationToken);

        if (material.MaterialType != MaterialType.RebarLinear)
        {
            throw new MaterialManagementException(
                "Rebar specification can only be assigned to RebarLinear materials.");
        }

        var spec = await _dbContext.RebarSpecs
            .AsNoTracking()
            .Include(x => x.Material)
            .SingleOrDefaultAsync(
                x => x.MaterialId == materialId,
                cancellationToken)
            ?? throw new MaterialManagementException(
                "Rebar specification was not found.");

        return Map(spec);
    }

    public async Task<RebarSpecResponse> CreateAsync(
        Guid currentUserId,
        CreateRebarSpecRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanAccess(actor);

        ValidateRequest(
            request.DiameterMm,
            request.Grade);

        var material = await GetMaterialAsync(
            actor,
            request.MaterialId,
            cancellationToken);

        if (material.MaterialType != MaterialType.RebarLinear)
        {
            throw new MaterialManagementException(
                "Rebar specification can only be assigned to RebarLinear materials.");
        }

        var exists = await _dbContext.RebarSpecs
            .AnyAsync(
                x => x.MaterialId == material.Id,
                cancellationToken);

        if (exists)
        {
            throw new MaterialManagementException(
                "A rebar specification already exists for this material.");
        }

        var spec = new RebarSpec
        {
            Id = Guid.NewGuid(),
            CompanyId = material.CompanyId,
            MaterialId = material.Id,
            DiameterMm = request.DiameterMm,
            Grade = request.Grade.Trim(),
            CreatedBy = currentUserId
        };

        _dbContext.RebarSpecs.Add(spec);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        spec.Material = material;

        return Map(spec);
    }

    public async Task<RebarSpecResponse> UpdateAsync(
        Guid currentUserId,
        Guid materialId,
        UpdateRebarSpecRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanAccess(actor);

        ValidateRequest(
            request.DiameterMm,
            request.Grade);

        var material = await GetMaterialAsync(
            actor,
            materialId,
            cancellationToken);

        if (material.MaterialType != MaterialType.RebarLinear)
        {
            throw new MaterialManagementException(
                "Rebar specification can only be assigned to RebarLinear materials.");
        }

        var spec = await _dbContext.RebarSpecs
            .SingleOrDefaultAsync(
                x => x.MaterialId == material.Id,
                cancellationToken)
            ?? throw new MaterialManagementException(
                "Rebar specification was not found.");

        spec.DiameterMm = request.DiameterMm;
        spec.Grade = request.Grade.Trim();
        spec.UpdatedBy = currentUserId;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        spec.Material = material;

        return Map(spec);
    }

    private async Task<Material> GetMaterialAsync(
        User actor,
        Guid materialId,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Materials
            .Include(x => x.RebarSpec)
            .Where(x => x.Id == materialId);

        if (!IsSuperAdmin(actor))
        {
            var companyId = GetActorCompanyId(actor);

            query = query.Where(
                x => x.CompanyId == companyId);
        }

        return await query.SingleOrDefaultAsync(
            cancellationToken)
            ?? throw new MaterialManagementException(
                "Material was not found.");
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
            ?? throw new MaterialManagementException(
                "Current user was not found or is inactive.");
    }

    private static void ValidateRequest(
        decimal diameterMm,
        string grade)
    {
        if (diameterMm <= 0m)
        {
            throw new MaterialManagementException(
                "Rebar diameter must be greater than zero.");
        }

        if (diameterMm > 1000m)
        {
            throw new MaterialManagementException(
                "Rebar diameter cannot exceed 1000 mm.");
        }

        if (string.IsNullOrWhiteSpace(grade))
        {
            throw new MaterialManagementException(
                "Rebar grade is required.");
        }

        if (grade.Trim().Length > 30)
        {
            throw new MaterialManagementException(
                "Rebar grade cannot exceed 30 characters.");
        }
    }

    private static RebarSpecResponse Map(
        RebarSpec spec)
    {
        return new RebarSpecResponse
        {
            Id = spec.Id,
            MaterialId = spec.MaterialId,
            MaterialCode = spec.Material.MaterialCode,
            MaterialName = spec.Material.Name,
            DiameterMm = spec.DiameterMm,
            Grade = spec.Grade
        };
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
            "You are not authorized to manage rebar specifications.");
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
}




