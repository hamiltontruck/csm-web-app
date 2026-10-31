using CSM.Application.Common.Exceptions;
using CSM.Application.Compliance.Permits;
using CSM.Application.Compliance.Permits.Dtos;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Compliance;
using CSM.Domain.Entities.Identity;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public sealed class PermitService : IPermitService
{
    private readonly ApplicationDbContext _dbContext;

    public PermitService(
        ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<PermitResponse>> GetAllAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanManage(actor);

        var query = ComplianceSiteAuthority.Records(_dbContext.Permits, _dbContext, actor)
            .AsNoTracking()
            .Include(x => x.ConstructionSite)
            .Where(x => !x.IsDeleted);

        return await query
            .OrderBy(x => x.PermitNumber)
            .Select(x => new PermitResponse
            {
                Id = x.Id,
                CompanyId = x.CompanyId,
                ConstructionSiteId = x.ConstructionSiteId,
                ConstructionSiteName = x.ConstructionSite.Name,
                ProjectId = x.ProjectId,
                PermitNumber = x.PermitNumber,
                PermitType = x.PermitType,
                Name = x.Name,
                IssuingAuthority = x.IssuingAuthority,
                ApplicationDate = x.ApplicationDate,
                IssueDate = x.IssueDate,
                ExpiryDate = x.ExpiryDate,
                Status = x.Status,
                ExpiryAlertDays = x.ExpiryAlertDays,
                Conditions = x.Conditions,
                Notes = x.Notes,
                CreatedAtUtc = x.CreatedAtUtc,
                UpdatedAtUtc = x.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<PermitResponse> GetByIdAsync(
        Guid currentUserId,
        Guid permitId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanManage(actor);

        var permit = await ComplianceSiteAuthority.Records(_dbContext.Permits, _dbContext, actor)
            .AsNoTracking()
            .Include(x => x.ConstructionSite)
            .SingleOrDefaultAsync(
                x => x.Id == permitId &&
                     !x.IsDeleted,
                cancellationToken)
            ?? throw new KeyNotFoundException(
                "Permit was not found.");

        EnsureCompanyAccess(
            actor,
            permit.CompanyId);

        return Map(permit);
    }

    public async Task<PermitResponse> CreateAsync(
        Guid currentUserId,
        CreatePermitRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanManage(actor);
        await ComplianceSiteAuthority.ManageAsync(_dbContext, actor, cancellationToken);

        var companyId = await ComplianceSiteAuthority.CreationCompanyAsync(_dbContext, actor, request.ConstructionSiteId, cancellationToken);

        Validate(
            request.PermitNumber,
            request.PermitType,
            request.Name,
            request.ExpiryAlertDays,
            request.ApplicationDate,
            request.IssueDate,
            request.ExpiryDate);

        await EnsureConstructionSiteAccess(
            actor,
            companyId,
            request.ConstructionSiteId,
            cancellationToken);

        await EnsureProjectAccess(
            companyId,
            request.ProjectId,
            request.ConstructionSiteId,
            cancellationToken);

        var normalizedPermitNumber =
            request.PermitNumber.Trim();

        var duplicate = await _dbContext.Permits
            .AnyAsync(
                x =>
                    !x.IsDeleted &&
                    x.CompanyId == companyId &&
                    x.PermitNumber == normalizedPermitNumber,
                cancellationToken);

        if (duplicate)
        {
            throw new PermitManagementException(
                "A permit with this permit number already exists in the company.");
        }

        var permit = new Permit
        {
            CompanyId = companyId,
            ConstructionSiteId = request.ConstructionSiteId,
            ProjectId = request.ProjectId,
            PermitNumber = normalizedPermitNumber,
            PermitType = request.PermitType.Trim(),
            Name = request.Name.Trim(),
            IssuingAuthority = Clean(request.IssuingAuthority),
            ApplicationDate = request.ApplicationDate,
            IssueDate = request.IssueDate,
            ExpiryDate = request.ExpiryDate,
            Status = request.Status,
            ExpiryAlertDays = request.ExpiryAlertDays,
            Conditions = Clean(request.Conditions),
            Notes = Clean(request.Notes)
        };

        _dbContext.Permits.Add(permit);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(permit);
    }

    public async Task<PermitResponse> UpdateAsync(
        Guid currentUserId,
        Guid permitId,
        UpdatePermitRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanManage(actor);
        await ComplianceSiteAuthority.ManageAsync(_dbContext, actor, cancellationToken);

        var permit = await ComplianceSiteAuthority.Records(_dbContext.Permits, _dbContext, actor)
            .SingleOrDefaultAsync(
                x =>
                    x.Id == permitId &&
                    !x.IsDeleted,
                cancellationToken)
            ?? throw new KeyNotFoundException(
                "Permit was not found.");

        EnsureCompanyAccess(
            actor,
            permit.CompanyId);

        Validate(
            request.PermitNumber,
            request.PermitType,
            request.Name,
            request.ExpiryAlertDays,
            request.ApplicationDate,
            request.IssueDate,
            request.ExpiryDate);

        await EnsureConstructionSiteAccess(
            actor,
            permit.CompanyId,
            request.ConstructionSiteId,
            cancellationToken);

        await EnsureProjectAccess(
            permit.CompanyId,
            request.ProjectId,
            request.ConstructionSiteId,
            cancellationToken);

        var normalizedPermitNumber =
            request.PermitNumber.Trim();

        var duplicate = await _dbContext.Permits
            .AnyAsync(
                x =>
                    x.Id != permit.Id &&
                    !x.IsDeleted &&
                    x.CompanyId == permit.CompanyId &&
                    x.PermitNumber == normalizedPermitNumber,
                cancellationToken);

        if (duplicate)
        {
            throw new PermitManagementException(
                "A permit with this permit number already exists in the company.");
        }

        permit.ConstructionSiteId =
            request.ConstructionSiteId;

        permit.ProjectId =
            request.ProjectId;

        permit.PermitNumber =
            normalizedPermitNumber;

        permit.PermitType =
            request.PermitType.Trim();

        permit.Name =
            request.Name.Trim();

        permit.IssuingAuthority =
            Clean(request.IssuingAuthority);

        permit.ApplicationDate =
            request.ApplicationDate;

        permit.IssueDate =
            request.IssueDate;

        permit.ExpiryDate =
            request.ExpiryDate;

        permit.Status =
            request.Status;

        permit.ExpiryAlertDays =
            request.ExpiryAlertDays;

        permit.Conditions =
            Clean(request.Conditions);

        permit.Notes =
            Clean(request.Notes);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(permit);
    }

    private async Task<User> GetActorAsync(
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        return await ComplianceSiteAuthority.ActorAsync(_dbContext, currentUserId, cancellationToken);
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

        throw new PermitManagementException(
            "You are not authorized to manage permits.");
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
            throw new PermitManagementException(
                "You are not authorized to access this permit.");
        }
    }

    private async Task EnsureConstructionSiteAccess(
        User actor,
        Guid companyId,
        Guid constructionSiteId,
        CancellationToken cancellationToken)
    {
        var sites = ComplianceSiteAuthority.Sites(_dbContext, actor);
        var exists = await sites
            .AnyAsync(
                x =>
                    x.Id == constructionSiteId &&
                    x.CompanyId == companyId &&
                    !x.IsDeleted,
                cancellationToken);

        if (!exists)
        {
            throw new KeyNotFoundException(
                "The specified construction site was not found in the company.");
        }
    }

    private async Task EnsureProjectAccess(
        Guid companyId,
        Guid? projectId,
        Guid constructionSiteId,
        CancellationToken cancellationToken)
    {
        if (!projectId.HasValue)
        {
            return;
        }

        var exists = await _dbContext.Projects
            .AnyAsync(
                x =>
                    x.Id == projectId.Value &&
                    x.CompanyId == companyId &&
                    x.ConstructionSiteId == constructionSiteId &&
                    !x.IsDeleted,
                cancellationToken);

        if (!exists)
        {
            throw new PermitManagementException(
                "The specified project was not found in the company.");
        }
    }

    private static Guid GetActorCompanyId(
        User actor)
    {
        if (!actor.CompanyId.HasValue)
        {
            throw new PermitManagementException(
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
        string permitNumber,
        string permitType,
        string name,
        int expiryAlertDays,
        DateOnly? applicationDate,
        DateOnly? issueDate,
        DateOnly? expiryDate)
    {
        if (string.IsNullOrWhiteSpace(permitNumber))
        {
            throw new PermitManagementException(
                "Permit number is required.");
        }

        if (permitNumber.Trim().Length > 100)
        {
            throw new PermitManagementException(
                "Permit number cannot exceed 100 characters.");
        }

        if (string.IsNullOrWhiteSpace(permitType))
        {
            throw new PermitManagementException(
                "Permit type is required.");
        }

        if (permitType.Trim().Length > 100)
        {
            throw new PermitManagementException(
                "Permit type cannot exceed 100 characters.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new PermitManagementException(
                "Permit name is required.");
        }

        if (name.Trim().Length > 200)
        {
            throw new PermitManagementException(
                "Permit name cannot exceed 200 characters.");
        }

        if (expiryAlertDays < 0)
        {
            throw new PermitManagementException(
                "Expiry alert days cannot be negative.");
        }

        if (applicationDate.HasValue &&
            issueDate.HasValue &&
            issueDate.Value < applicationDate.Value)
        {
            throw new PermitManagementException(
                "Issue date cannot be earlier than application date.");
        }

        if (issueDate.HasValue &&
            expiryDate.HasValue &&
            expiryDate.Value < issueDate.Value)
        {
            throw new PermitManagementException(
                "Expiry date cannot be earlier than issue date.");
        }
    }

    private static string? Clean(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static PermitResponse Map(Permit permit)
    {
        return new PermitResponse
        {
            Id = permit.Id,
            CompanyId = permit.CompanyId,
            ConstructionSiteId = permit.ConstructionSiteId,
            ConstructionSiteName = permit.ConstructionSite?.Name ?? string.Empty,
            ProjectId = permit.ProjectId,
            PermitNumber = permit.PermitNumber,
            PermitType = permit.PermitType,
            Name = permit.Name,
            IssuingAuthority = permit.IssuingAuthority,
            ApplicationDate = permit.ApplicationDate,
            IssueDate = permit.IssueDate,
            ExpiryDate = permit.ExpiryDate,
            Status = permit.Status,
            ExpiryAlertDays = permit.ExpiryAlertDays,
            Conditions = permit.Conditions,
            Notes = permit.Notes,
            CreatedAtUtc = permit.CreatedAtUtc,
            UpdatedAtUtc = permit.UpdatedAtUtc
        };
    }
}
