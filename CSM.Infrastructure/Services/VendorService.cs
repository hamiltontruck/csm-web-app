using CSM.Application.Common.Exceptions;
using CSM.Application.Procurement.Vendors;
using CSM.Application.Procurement.Vendors.Dtos;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Finance;
using CSM.Domain.Entities.Identity;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public sealed class VendorService : IVendorService
{
    private readonly ApplicationDbContext _dbContext;

    public VendorService(
        ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<VendorResponse>> GetAllAsync(
        Guid currentUserId,
        bool? isActive,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanManage(actor);

        var query = _dbContext.Vendors
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

        return await query
            .OrderBy(x => x.VendorCode)
            .Select(
                x => new VendorResponse
                {
                    Id = x.Id,
                    CompanyId = x.CompanyId,
                    VendorCode = x.VendorCode,
                    Name = x.Name,
                    ContactPerson = x.ContactPerson,
                    PhoneNumber = x.PhoneNumber,
                    Email = x.Email,
                    TaxIdentificationNumber =
                        x.TaxIdentificationNumber,
                    RegistrationNumber =
                        x.RegistrationNumber,
                    BankName = x.BankName,
                    BankAccountNumber =
                        x.BankAccountNumber,
                    Address = x.Address,
                    IsActive = x.IsActive,
                    CreatedAtUtc = x.CreatedAtUtc,
                    UpdatedAtUtc = x.UpdatedAtUtc
                })
            .ToListAsync(cancellationToken);
    }

    public async Task<VendorResponse> GetByIdAsync(
        Guid currentUserId,
        Guid vendorId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanManage(actor);

        var vendor = await _dbContext.Vendors
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == vendorId,
                cancellationToken)
            ?? throw new VendorManagementException(
                "Vendor was not found.");

        EnsureCompanyAccess(actor, vendor.CompanyId);

        return Map(vendor);
    }

    public async Task<VendorResponse> CreateAsync(
        Guid currentUserId,
        CreateVendorRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanManage(actor);

        var companyId = GetActorCompanyId(actor);

        Validate(
            request.VendorCode,
            request.Name);

        var normalizedCode =
            request.VendorCode.Trim().ToUpperInvariant();

        var duplicate = await _dbContext.Vendors
            .AnyAsync(
                x =>
                    x.CompanyId == companyId &&
                    x.VendorCode.ToUpper() == normalizedCode,
                cancellationToken);

        if (duplicate)
        {
            throw new VendorManagementException(
                "A vendor with this vendor code already exists in the company.");
        }

        var vendor = new Vendor
        {
            CompanyId = companyId,
            VendorCode = normalizedCode,
            Name = request.Name.Trim(),
            ContactPerson = Clean(request.ContactPerson),
            PhoneNumber = Clean(request.PhoneNumber),
            Email = Clean(request.Email),
            TaxIdentificationNumber =
                Clean(request.TaxIdentificationNumber),
            RegistrationNumber =
                Clean(request.RegistrationNumber),
            BankName = Clean(request.BankName),
            BankAccountNumber =
                Clean(request.BankAccountNumber),
            Address = Clean(request.Address),
            IsActive = request.IsActive
        };

        _dbContext.Vendors.Add(vendor);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(vendor);
    }

    public async Task<VendorResponse> UpdateAsync(
        Guid currentUserId,
        Guid vendorId,
        UpdateVendorRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanManage(actor);

        var vendor = await _dbContext.Vendors
            .SingleOrDefaultAsync(
                x => x.Id == vendorId,
                cancellationToken)
            ?? throw new VendorManagementException(
                "Vendor was not found.");

        EnsureCompanyAccess(actor, vendor.CompanyId);

        Validate(
            request.VendorCode,
            request.Name);

        var normalizedCode =
            request.VendorCode.Trim().ToUpperInvariant();

        var duplicate = await _dbContext.Vendors
            .AnyAsync(
                x =>
                    x.Id != vendor.Id &&
                    x.CompanyId == vendor.CompanyId &&
                    x.VendorCode.ToUpper() == normalizedCode,
                cancellationToken);

        if (duplicate)
        {
            throw new VendorManagementException(
                "A vendor with this vendor code already exists in the company.");
        }

        vendor.VendorCode = normalizedCode;
        vendor.Name = request.Name.Trim();
        vendor.ContactPerson =
            Clean(request.ContactPerson);
        vendor.PhoneNumber =
            Clean(request.PhoneNumber);
        vendor.Email =
            Clean(request.Email);
        vendor.TaxIdentificationNumber =
            Clean(request.TaxIdentificationNumber);
        vendor.RegistrationNumber =
            Clean(request.RegistrationNumber);
        vendor.BankName =
            Clean(request.BankName);
        vendor.BankAccountNumber =
            Clean(request.BankAccountNumber);
        vendor.Address =
            Clean(request.Address);
        vendor.IsActive =
            request.IsActive;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(vendor);
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
            ?? throw new VendorManagementException(
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

        throw new VendorManagementException(
            "You are not authorized to manage vendors.");
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
            throw new VendorManagementException(
                "You are not authorized to access this vendor.");
        }
    }

    private static Guid GetActorCompanyId(
        User actor)
    {
        if (!actor.CompanyId.HasValue)
        {
            throw new VendorManagementException(
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
        string vendorCode,
        string name)
    {
        if (string.IsNullOrWhiteSpace(vendorCode))
        {
            throw new VendorManagementException(
                "Vendor code is required.");
        }

        if (vendorCode.Trim().Length > 30)
        {
            throw new VendorManagementException(
                "Vendor code cannot exceed 30 characters.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new VendorManagementException(
                "Vendor name is required.");
        }

        if (name.Trim().Length > 200)
        {
            throw new VendorManagementException(
                "Vendor name cannot exceed 200 characters.");
        }
    }

    private static VendorResponse Map(
        Vendor vendor)
    {
        return new VendorResponse
        {
            Id = vendor.Id,
            CompanyId = vendor.CompanyId,
            VendorCode = vendor.VendorCode,
            Name = vendor.Name,
            ContactPerson = vendor.ContactPerson,
            PhoneNumber = vendor.PhoneNumber,
            Email = vendor.Email,
            TaxIdentificationNumber =
                vendor.TaxIdentificationNumber,
            RegistrationNumber =
                vendor.RegistrationNumber,
            BankName = vendor.BankName,
            BankAccountNumber =
                vendor.BankAccountNumber,
            Address = vendor.Address,
            IsActive = vendor.IsActive,
            CreatedAtUtc = vendor.CreatedAtUtc,
            UpdatedAtUtc = vendor.UpdatedAtUtc
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
