using CSM.Application.Common.Exceptions;
using CSM.Application.Finance.CostCodes;
using CSM.Application.Finance.CostCodes.Dtos;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Finance;
using CSM.Domain.Entities.Identity;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public sealed class CostCodeService : ICostCodeService
{
    private readonly ApplicationDbContext _dbContext;

    public CostCodeService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<CostCodeResponse>> GetAllAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanRead(actor);

        IQueryable<CostCode> query = _dbContext.CostCodes
            .Include(x => x.ParentCostCode)
            .AsNoTracking();

        if (!IsSuperAdmin(actor))
        {
            query = query.Where(
                x => x.CompanyId == GetActorCompanyId(actor));
        }

        var costCodes = await query
            .OrderBy(x => x.Code)
            .ToListAsync(cancellationToken);

        return costCodes
            .Select(Map)
            .ToArray();
    }

    public async Task<IReadOnlyCollection<CostCodeResponse>> GetActiveAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanRead(actor);

        IQueryable<CostCode> query =
            _dbContext.CostCodes
                .AsNoTracking()
                .Where(x => x.IsActive && !x.IsDeleted);

        if (!IsSuperAdmin(actor))
        {
            query = query.Where(
                x => x.CompanyId == GetActorCompanyId(actor));
        }

        var costCodes = await query
            .OrderBy(x => x.Code)
            .ToListAsync(cancellationToken);

        return costCodes
            .Select(Map)
            .ToArray();
    }

    public async Task<CostCodeResponse> GetByIdAsync(
        Guid currentUserId,
        Guid costCodeId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanRead(actor);

        var costCode = await GetCostCodeAsync(
            costCodeId,
            cancellationToken);

        EnsureCompanyAccess(actor, costCode.CompanyId);

        return Map(costCode);
    }

    public async Task<CostCodeResponse> CreateAsync(
        Guid currentUserId,
        CreateCostCodeRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanModify(actor);

        var companyId = GetActorCompanyId(actor);

        var code = CleanRequired(
            request.Code,
            "Cost code");

        var name = CleanRequired(
            request.Name,
            "Cost code name");

        if (code.Length > 30)
        {
            throw new CostCodeManagementException(
                "Cost code cannot exceed 30 characters.");
        }

        if (name.Length > 200)
        {
            throw new CostCodeManagementException(
                "Cost code name cannot exceed 200 characters.");
        }

        await EnsureParentAccess(
            companyId,
            request.ParentCostCodeId,
            cancellationToken);

        if (request.ParentCostCodeId == null)
        {
            // No parent is valid.
        }

        var duplicateExists = await _dbContext.CostCodes
            .AnyAsync(
                x =>
                    x.CompanyId == companyId &&
                    x.Code == code,
                cancellationToken);

        if (duplicateExists)
        {
            throw new CostCodeManagementException(
                $"Cost code '{code}' already exists.");
        }

        var costCode = new CostCode
        {
            CompanyId = companyId,
            Code = code,
            Name = name,
            Description = Clean(request.Description),
            ParentCostCodeId = request.ParentCostCodeId,
            IsActive = true
        };

        _dbContext.CostCodes.Add(costCode);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(costCode);
    }

    public async Task<CostCodeResponse> UpdateAsync(
        Guid currentUserId,
        Guid costCodeId,
        UpdateCostCodeRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanModify(actor);

        var costCode = await GetCostCodeAsync(
            costCodeId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            costCode.CompanyId);

        var name = CleanRequired(
            request.Name,
            "Cost code name");

        if (name.Length > 200)
        {
            throw new CostCodeManagementException(
                "Cost code name cannot exceed 200 characters.");
        }

        if (request.ParentCostCodeId == costCode.Id)
        {
            throw new CostCodeManagementException(
                "A cost code cannot be its own parent.");
        }

        await EnsureParentAccess(
            costCode.CompanyId,
            request.ParentCostCodeId,
            cancellationToken);

        if (request.ParentCostCodeId.HasValue)
        {
            await EnsureNoCircularParent(
                costCode.Id,
                request.ParentCostCodeId.Value,
                cancellationToken);
        }

        costCode.Name = name;
        costCode.Description =
            Clean(request.Description);
        costCode.ParentCostCodeId =
            request.ParentCostCodeId;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(costCode);
    }

    public async Task<CostCodeResponse> ChangeActiveStatusAsync(
        Guid currentUserId,
        Guid costCodeId,
        ChangeCostCodeActiveStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanModify(actor);

        var costCode = await GetCostCodeAsync(
            costCodeId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            costCode.CompanyId);

        costCode.IsActive = request.IsActive;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(costCode);
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
            ?? throw new CostCodeManagementException(
                "Authenticated user was not found.");
    }

    private static void EnsureCanRead(User actor)
    {
        if (IsSuperAdmin(actor) ||
            HasRole(actor, AppRoles.CompanyAdmin) ||
            HasRole(actor, AppRoles.Accountant))
        {
            return;
        }

        throw new CostCodeManagementException(
            "You are not authorized to view cost codes.");
    }

    private static void EnsureCanModify(User actor)
    {
        if (IsSuperAdmin(actor) ||
            HasRole(actor, AppRoles.CompanyAdmin) ||
            HasRole(actor, AppRoles.Accountant))
        {
            return;
        }

        throw new CostCodeManagementException(
            "You are not authorized to manage cost codes.");
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
            throw new CostCodeManagementException(
                "You are not authorized to access this cost code.");
        }
    }

    private static Guid GetActorCompanyId(User actor)
    {
        if (!actor.CompanyId.HasValue)
        {
            throw new CostCodeManagementException(
                "Current user is not assigned to a company.");
        }

        return actor.CompanyId.Value;
    }

    private async Task<CostCode> GetCostCodeAsync(
        Guid costCodeId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.CostCodes
            .SingleOrDefaultAsync(
                x => x.Id == costCodeId &&
                     !x.IsDeleted,
                cancellationToken)
            ?? throw new CostCodeManagementException(
                "Cost code was not found.");
    }

    private async Task EnsureParentAccess(
        Guid companyId,
        Guid? parentCostCodeId,
        CancellationToken cancellationToken)
    {
        if (!parentCostCodeId.HasValue)
        {
            return;
        }

        var parentExists = await _dbContext.CostCodes
            .AnyAsync(
                x =>
                    x.Id == parentCostCodeId.Value &&
                    x.CompanyId == companyId &&
                    !x.IsDeleted,
                cancellationToken);

        if (!parentExists)
        {
            throw new CostCodeManagementException(
                "The selected parent cost code is invalid or inaccessible.");
        }
    }

    private async Task EnsureNoCircularParent(
        Guid costCodeId,
        Guid parentCostCodeId,
        CancellationToken cancellationToken)
    {
        var visited = new HashSet<Guid>();
        var currentId = parentCostCodeId;

        while (true)
        {
            if (!visited.Add(currentId))
            {
                throw new CostCodeManagementException(
                    "The selected parent hierarchy contains a cycle.");
            }

            if (currentId == costCodeId)
            {
                throw new CostCodeManagementException(
                    "The selected parent would create a circular cost code hierarchy.");
            }

            var parentId = await _dbContext.CostCodes
                .Where(x => x.Id == currentId && !x.IsDeleted)
                .Select(x => x.ParentCostCodeId)
                .SingleOrDefaultAsync(cancellationToken);

            if (!parentId.HasValue)
            {
                return;
            }

            currentId = parentId.Value;
        }
    }

    public async Task<List<CostCodeResponse>> GetBySiteAsync(
        Guid siteId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.CostCodes
            .Where(x =>
                (x.ConstructionSiteId == null ||
                 x.ConstructionSiteId == siteId) &&
                !x.IsDeleted)
            .OrderBy(x => x.Code)
            .Select(x => new CostCodeResponse(
                x.Id,
                x.CompanyId,
                x.ConstructionSiteId,
                x.ParentCostCode != null ? x.ParentCostCode.Code : null,
                x.ParentCostCode != null ? x.ParentCostCode.Name : null,
                x.Code,
                x.Name,
                x.Description,
                x.ParentCostCodeId,
                x.IsActive,
                x.CreatedAtUtc,
                x.UpdatedAtUtc
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        Guid currentUserId,
        Guid costCodeId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(currentUserId, cancellationToken);

        EnsureCanModify(actor);

        var costCode = await _dbContext.CostCodes
            .SingleOrDefaultAsync(x =>
                x.Id == costCodeId &&
                !x.IsDeleted,
                cancellationToken)
            ?? throw new CostCodeManagementException("Cost code not found.");

        EnsureCompanyAccess(actor, costCode.CompanyId);

        var inUse = await _dbContext.BudgetLines.AnyAsync(
            x => x.CostCodeId == costCodeId && !x.IsDeleted,
            cancellationToken);

        if (inUse)
        {
            throw new CostCodeManagementException(
                "This cost code is already used by one or more budget lines and cannot be deleted.");
        }

        costCode.IsDeleted = true;
        costCode.DeletedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string CleanRequired(
        string? value,
        string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new CostCodeManagementException(
                $"{fieldName} is required.");
        }

        return value.Trim();
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static bool IsSuperAdmin(User actor)
    {
        return HasRole(actor, AppRoles.SuperAdmin);
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

    private static CostCodeResponse Map(CostCode costCode)
    {
        return new CostCodeResponse(
            costCode.Id,
            costCode.CompanyId,
            costCode.ConstructionSiteId,
            costCode.ParentCostCode?.Code,
            costCode.ParentCostCode?.Name,
            costCode.Code,
            costCode.Name,
            costCode.Description,
            costCode.ParentCostCodeId,
            costCode.IsActive,
            costCode.CreatedAtUtc,
            costCode.UpdatedAtUtc);
    }
}
