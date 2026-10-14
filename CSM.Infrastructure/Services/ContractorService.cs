using CSM.Application.Common.Exceptions;
using CSM.Application.Common.Security;
using CSM.Application.HRM.Contractors;
using CSM.Application.HRM.Contractors.Dtos;
using CSM.Domain.Constants;
using CSM.Domain.Entities.HRM;
using CSM.Domain.Entities.Identity;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using CSM.Domain.Enums;
using CSM.Infrastructure.Security;

namespace CSM.Infrastructure.Services;

public sealed class ContractorService : IContractorService
{
    private readonly ApplicationDbContext _dbContext;

    private readonly IPermissionEvaluator _permissionEvaluator;

    public ContractorService(
        ApplicationDbContext dbContext,
        IPermissionEvaluator permissionEvaluator)
    {
        _dbContext = dbContext;
        _permissionEvaluator = permissionEvaluator;
    }

    public async Task<IReadOnlyCollection<ContractorResponse>> GetAllAsync(
        Guid currentUserId,
        bool? isActive,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        var canRead =
            await _permissionEvaluator.HasPermissionAsync(
                currentUserId,
                GranularPermission.HRM_Contractor_View.ToString(),
                cancellationToken);

        if (!canRead)
        {
            throw new ContractorManagementException(
                "You are not authorized to access contractors.");
        }

        var query = _dbContext.Contractors
            .AsNoTracking()
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

        var contractors = await query
            .OrderBy(x => x.ContractorCode)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return contractors
            .Select(Map)
            .ToList();
    }

    public async Task<ContractorResponse> GetByIdAsync(
        Guid currentUserId,
        Guid contractorId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanRead(actor);

        var contractor = await GetContractorAsync(
            contractorId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            contractor.CompanyId);

        return Map(contractor);
    }

    public async Task<ContractorResponse> CreateAsync(
        Guid currentUserId,
        CreateContractorRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanManage(actor);

        var companyId = GetActorCompanyId(actor);

        var contractorCode = Clean(request.ContractorCode);
        var name = Clean(request.Name);

        if (contractorCode is null)
        {
            throw new ContractorManagementException(
                "Contractor code is required.");
        }

        if (name is null)
        {
            throw new ContractorManagementException(
                "Contractor name is required.");
        }

        if (contractorCode.Length > 30)
        {
            throw new ContractorManagementException(
                "Contractor code cannot exceed 30 characters.");
        }

        if (name.Length > 200)
        {
            throw new ContractorManagementException(
                "Contractor name cannot exceed 200 characters.");
        }

        var duplicateExists =
            await _dbContext.Contractors
                .AnyAsync(
                    x =>
                        x.CompanyId == companyId &&
                        x.ContractorCode == contractorCode,
                    cancellationToken);

        if (duplicateExists)
        {
            throw new ContractorManagementException(
                "A contractor with this code already exists.");
        }

        var contractor = new Contractor
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            ContractorCode = contractorCode,
            Name = name,
            ContactPerson = Clean(request.ContactPerson),
            PhoneNumber = Clean(request.PhoneNumber),
            Email = Clean(request.Email),
            TaxIdentificationNumber =
                Clean(request.TaxIdentificationNumber),
            RegistrationNumber =
                Clean(request.RegistrationNumber),
            Address = Clean(request.Address),
            IsActive = request.IsActive,
            CreatedBy = currentUserId
        };

        _dbContext.Contractors.Add(contractor);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(contractor);
    }

    public async Task<ContractorResponse> UpdateAsync(
        Guid currentUserId,
        Guid contractorId,
        UpdateContractorRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanManage(actor);

        var contractor = await GetContractorAsync(
            contractorId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            contractor.CompanyId);

        var contractorCode = Clean(request.ContractorCode);
        var name = Clean(request.Name);

        if (contractorCode is null)
        {
            throw new ContractorManagementException(
                "Contractor code is required.");
        }

        if (name is null)
        {
            throw new ContractorManagementException(
                "Contractor name is required.");
        }

        if (contractorCode.Length > 30)
        {
            throw new ContractorManagementException(
                "Contractor code cannot exceed 30 characters.");
        }

        if (name.Length > 200)
        {
            throw new ContractorManagementException(
                "Contractor name cannot exceed 200 characters.");
        }

        var duplicateExists =
            await _dbContext.Contractors
                .AnyAsync(
                    x =>
                        x.Id != contractor.Id &&
                        x.CompanyId == contractor.CompanyId &&
                        x.ContractorCode == contractorCode,
                    cancellationToken);

        if (duplicateExists)
        {
            throw new ContractorManagementException(
                "A contractor with this code already exists.");
        }

        contractor.ContractorCode = contractorCode;
        contractor.Name = name;
        contractor.ContactPerson =
            Clean(request.ContactPerson);
        contractor.PhoneNumber =
            Clean(request.PhoneNumber);
        contractor.Email =
            Clean(request.Email);
        contractor.TaxIdentificationNumber =
            Clean(request.TaxIdentificationNumber);
        contractor.RegistrationNumber =
            Clean(request.RegistrationNumber);
        contractor.Address =
            Clean(request.Address);
        contractor.IsActive = request.IsActive;
        contractor.UpdatedBy = currentUserId;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(contractor);
    }

    private async Task<User> GetActorAsync(
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Users
            .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
            .FirstOrDefaultAsync(
                x => x.Id == currentUserId,
                cancellationToken)
            ?? throw new ContractorManagementException(
                "Current user was not found.");
    }

    private async Task<Contractor> GetContractorAsync(
        Guid contractorId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Contractors
            .FirstOrDefaultAsync(
                x => x.Id == contractorId,
                cancellationToken)
            ?? throw new ContractorManagementException(
                "Contractor was not found.");
    }

    private static void EnsureCanRead(
        User actor)
    {
        if (IsSuperAdmin(actor) ||
            HasRole(actor, AppRoles.CompanyAdmin) ||
            HasRole(actor, AppRoles.SiteManager) ||
            HasRole(actor, AppRoles.Supervisor))
        {
            return;
        }

        throw new ContractorManagementException(
            "You are not authorized to access contractors.");
    }

    private static void EnsureCanManage(
        User actor)
    {
        if (IsSuperAdmin(actor) ||
            HasRole(actor, AppRoles.CompanyAdmin))
        {
            return;
        }

        throw new ContractorManagementException(
            "You are not authorized to manage contractors.");
    }

    private static Guid GetActorCompanyId(
        User actor)
    {
        if (!actor.CompanyId.HasValue)
        {
            throw new ContractorManagementException(
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
            throw new ContractorManagementException(
                "You cannot access contractors outside your company.");
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

    private static ContractorResponse Map(
        Contractor contractor)
    {
        return new ContractorResponse
        {
            Id = contractor.Id,
            CompanyId = contractor.CompanyId,
            ContractorCode = contractor.ContractorCode,
            Name = contractor.Name,
            ContactPerson = contractor.ContactPerson,
            PhoneNumber = contractor.PhoneNumber,
            Email = contractor.Email,
            TaxIdentificationNumber =
                contractor.TaxIdentificationNumber,
            RegistrationNumber =
                contractor.RegistrationNumber,
            Address = contractor.Address,
            IsActive = contractor.IsActive,
            CreatedAtUtc = contractor.CreatedAtUtc,
            UpdatedAtUtc = contractor.UpdatedAtUtc
        };
    }
}
