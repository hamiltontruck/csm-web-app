using CSM.Application.Common.Exceptions;
using CSM.Application.Common.Security;
using CSM.Application.Compliance.DocumentCategories;
using CSM.Application.Compliance.DocumentCategories.Dtos;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Compliance;
using CSM.Domain.Entities.Identity;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public sealed class DocumentCategoryService
    : IDocumentCategoryService
{
    private readonly ApplicationDbContext _dbContext;

    public DocumentCategoryService(
        ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<DocumentCategoryResponse>> GetAllAsync(
        Guid currentUserId,
        bool? isActive,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanManage(actor);

        var query = _dbContext.DocumentCategories
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
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

        return await query
            .OrderBy(x => x.CategoryCode)
            .Select(
                x => new DocumentCategoryResponse
                {
                    Id = x.Id,
                    CompanyId = x.CompanyId,
                    CategoryCode = x.CategoryCode,
                    Name = x.Name,
                    Description = x.Description,
                    IsActive = x.IsActive,
                    CreatedAtUtc = x.CreatedAtUtc,
                    UpdatedAtUtc = x.UpdatedAtUtc
                })
            .ToListAsync(cancellationToken);
    }

    public async Task<DocumentCategoryResponse> GetByIdAsync(
        Guid currentUserId,
        Guid documentCategoryId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanManage(actor);

        var category = await _dbContext.DocumentCategories
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x =>
                    x.Id == documentCategoryId &&
                    !x.IsDeleted,
                cancellationToken)
            ?? throw new DocumentCategoryManagementException(
                "Document category was not found.");

        EnsureCompanyAccess(
            actor,
            category.CompanyId);

        return Map(category);
    }

    public async Task<DocumentCategoryResponse> CreateAsync(
        Guid currentUserId,
        CreateDocumentCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanManage(actor);

        var companyId = GetActorCompanyId(actor);

        Validate(
            request.CategoryCode,
            request.Name);

        var normalizedCode =
            request.CategoryCode
                .Trim()
                .ToUpperInvariant();

        var duplicate = await _dbContext.DocumentCategories
            .AnyAsync(
                x =>
                    !x.IsDeleted &&
                    x.CompanyId == companyId &&
                    x.CategoryCode.ToUpper() == normalizedCode,
                cancellationToken);

        if (duplicate)
        {
            throw new DocumentCategoryManagementException(
                "A document category with this code already exists in the company.");
        }

        var category = new DocumentCategory
        {
            CompanyId = companyId,
            CategoryCode = normalizedCode,
            Name = request.Name.Trim(),
            Description = Clean(request.Description),
            IsActive = request.IsActive
        };

        _dbContext.DocumentCategories.Add(category);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(category);
    }

    public async Task<DocumentCategoryResponse> UpdateAsync(
        Guid currentUserId,
        Guid documentCategoryId,
        UpdateDocumentCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanManage(actor);

        var category = await _dbContext.DocumentCategories
            .SingleOrDefaultAsync(
                x =>
                    x.Id == documentCategoryId &&
                    !x.IsDeleted,
                cancellationToken)
            ?? throw new DocumentCategoryManagementException(
                "Document category was not found.");

        EnsureCompanyAccess(
            actor,
            category.CompanyId);

        Validate(
            request.CategoryCode,
            request.Name);

        var normalizedCode =
            request.CategoryCode
                .Trim()
                .ToUpperInvariant();

        var duplicate = await _dbContext.DocumentCategories
            .AnyAsync(
                x =>
                    x.Id != category.Id &&
                    !x.IsDeleted &&
                    x.CompanyId == category.CompanyId &&
                    x.CategoryCode.ToUpper() == normalizedCode,
                cancellationToken);

        if (duplicate)
        {
            throw new DocumentCategoryManagementException(
                "A document category with this code already exists in the company.");
        }

        category.CategoryCode = normalizedCode;
        category.Name = request.Name.Trim();
        category.Description = Clean(request.Description);
        category.IsActive = request.IsActive;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(category);
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
            ?? throw new DocumentCategoryManagementException(
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

        throw new DocumentCategoryManagementException(
            "You are not authorized to manage document categories.");
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
            throw new DocumentCategoryManagementException(
                "You are not authorized to access this document category.");
        }
    }

    private static Guid GetActorCompanyId(
        User actor)
    {
        if (!actor.CompanyId.HasValue)
        {
            throw new DocumentCategoryManagementException(
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

    private static void Validate(
        string categoryCode,
        string name)
    {
        if (string.IsNullOrWhiteSpace(categoryCode))
        {
            throw new DocumentCategoryManagementException(
                "Document category code is required.");
        }

        if (categoryCode.Trim().Length > 30)
        {
            throw new DocumentCategoryManagementException(
                "Document category code cannot exceed 30 characters.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DocumentCategoryManagementException(
                "Document category name is required.");
        }

        if (name.Trim().Length > 200)
        {
            throw new DocumentCategoryManagementException(
                "Document category name cannot exceed 200 characters.");
        }
    }

    private static DocumentCategoryResponse Map(
        DocumentCategory category)
    {
        return new DocumentCategoryResponse
        {
            Id = category.Id,
            CompanyId = category.CompanyId,
            CategoryCode = category.CategoryCode,
            Name = category.Name,
            Description = category.Description,
            IsActive = category.IsActive,
            CreatedAtUtc = category.CreatedAtUtc,
            UpdatedAtUtc = category.UpdatedAtUtc
        };
    }

    private static string? Clean(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
