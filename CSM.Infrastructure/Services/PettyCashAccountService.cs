using CSM.Application.Common.Exceptions;
using CSM.Application.Finance.PettyCashAccounts;
using CSM.Application.Finance.PettyCashAccounts.Dtos;
using CSM.Domain.Entities.Finance;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using CSM.Domain.Constants;


namespace CSM.Infrastructure.Services;

public sealed class PettyCashAccountService : IPettyCashAccountService
{
    private readonly ApplicationDbContext _dbContext;

    public PettyCashAccountService(
        ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<PettyCashAccountResponse>> GetAllAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        if (HasRole(actor, AppRoles.SiteManager))
        {
            throw new PettyCashManagementException(
                "Site Managers must access petty cash through their assigned site.");
        }

        var query = _dbContext.PettyCashAccounts
            .AsNoTracking()
            .Include(x => x.ConstructionSite)
            .Where(x => !x.IsDeleted)
            .AsQueryable();

        if (!IsSuperAdmin(actor))
        {
            if (!actor.CompanyId.HasValue)
            {
                throw new PettyCashManagementException(
                    "The current user is not associated with a company.");
            }

            query = query.Where(
                x => x.CompanyId == actor.CompanyId.Value);
        }

        var accounts = await query
            .OrderBy(x => x.AccountCode)
            .ToListAsync(cancellationToken);

        return accounts
            .Select(Map)
            .ToList();
    }

    public async Task<IReadOnlyCollection<PettyCashAccountResponse>> GetSiteAccountsAsync(
        Guid currentUserId,
        Guid siteId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        var site = await _dbContext.ConstructionSites
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x =>
                    x.Id == siteId &&
                    !x.IsDeleted,
                cancellationToken)
            ?? throw new PettyCashManagementException(
                "Construction site not found.");

        if (IsSuperAdmin(actor))
        {
            // SuperAdmin may access any site.
        }
        else
        {
            if (site.CompanyId != actor.CompanyId)
            {
                throw new PettyCashManagementException(
                    "You are not authorized to access this petty cash account.");
            }

            if (HasRole(actor, AppRoles.SiteManager))
            {
                var hasSiteAccess = await _dbContext.UserSiteAccesses
                    .AnyAsync(
                        x =>
                            x.UserId == actor.Id &&
                            x.SiteId == siteId &&
                            x.CompanyId == actor.CompanyId &&
                            x.IsActive,
                        cancellationToken);

                if (!hasSiteAccess)
                {
                    throw new PettyCashManagementException(
                        "You are not assigned to this site's petty cash scope.");
                }
            }
        }

        var accounts = await _dbContext.PettyCashAccounts
            .AsNoTracking()
            .Include(x => x.ConstructionSite)
            .Where(
                x =>
                    x.CompanyId == site.CompanyId &&
                    x.ConstructionSiteId == siteId &&
                    !x.IsDeleted)
            .OrderBy(x => x.AccountCode)
            .ToListAsync(cancellationToken);

        return accounts
            .Select(Map)
            .ToList();
    }

    public async Task<PettyCashAccountResponse> GetByIdAsync(
        Guid currentUserId,
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        var account = await _dbContext.PettyCashAccounts
            .AsNoTracking()
            .Include(x => x.ConstructionSite)
            .SingleOrDefaultAsync(
                x => x.Id == accountId,
                cancellationToken)
            ?? throw new PettyCashManagementException(
                "Petty cash account not found.");

        if (IsSuperAdmin(actor))
        {
            return Map(account);
        }

        if (account.CompanyId != actor.CompanyId)
        {
            throw new PettyCashManagementException(
                "You are not authorized to access this petty cash account.");
        }

        if (HasRole(actor, AppRoles.SiteManager))
        {
            if (!account.ConstructionSiteId.HasValue)
            {
                throw new PettyCashManagementException(
                    "You are not authorized to access this petty cash account.");
            }

            var hasSiteAccess = await _dbContext.UserSiteAccesses
                .AnyAsync(
                    x =>
                        x.UserId == actor.Id &&
                        x.SiteId == account.ConstructionSiteId.Value &&
                        x.CompanyId == actor.CompanyId &&
                        x.IsActive,
                    cancellationToken);

            if (!hasSiteAccess)
            {
                throw new PettyCashManagementException(
                    "You are not assigned to this site's petty cash scope.");
            }
        }

        return Map(account);
    }

    public async Task<PettyCashAccountResponse> CreateAsync(
        Guid currentUserId,
        CreatePettyCashAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        Guid companyId;
        ConstructionSite? site = null;

        if (request.ConstructionSiteId.HasValue)
        {
            site = await _dbContext.ConstructionSites
                .SingleOrDefaultAsync(
                    x =>
                        x.Id == request.ConstructionSiteId.Value &&
                        !x.IsDeleted,
                    cancellationToken)
                ?? throw new PettyCashManagementException(
                    "Construction site not found.");

            companyId = site.CompanyId;

            if (IsSuperAdmin(actor))
            {
                // SuperAdmin may create for any company/site.
            }
            else
            {
                if (companyId != actor.CompanyId)
                {
                    throw new PettyCashManagementException(
                        "You are not authorized to access this petty cash account.");
                }

                if (HasRole(actor, AppRoles.SiteManager))
                {
                    var hasSiteAccess = await _dbContext.UserSiteAccesses
                        .AnyAsync(
                            x =>
                                x.UserId == actor.Id &&
                                x.SiteId == site.Id &&
                                x.CompanyId == actor.CompanyId &&
                                x.IsActive,
                            cancellationToken);

                    if (!hasSiteAccess)
                    {
                        throw new PettyCashManagementException(
                            "You are not assigned to this site's petty cash scope.");
                    }
                }
            }
        }
        else
        {
            if (actor.CompanyId == null)
            {
                throw new PettyCashManagementException(
                    "The current user is not associated with a company.");
            }

            if (HasRole(actor, AppRoles.SiteManager))
            {
                throw new PettyCashManagementException(
                    "Site Managers cannot create company-level petty cash accounts.");
            }

            companyId = actor.CompanyId.Value;
        }

        var exists = await _dbContext.PettyCashAccounts.AnyAsync(
            x =>
                x.CompanyId == companyId &&
                x.AccountCode == request.AccountCode,
            cancellationToken);

        if (exists)
        {
            throw new PettyCashManagementException(
                "Account code already exists.");
        }

        var account = new PettyCashAccount
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            ConstructionSiteId = site?.Id,
            AccountCode = request.AccountCode,
            AccountName = request.AccountName,
            OpeningBalance = request.OpeningBalance,
            CurrentBalance = request.OpeningBalance,
            IsActive = true,
            CreatedBy = currentUserId
        };

        _dbContext.PettyCashAccounts.Add(account);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        account.ConstructionSite = site;

        return Map(account);
    }

    public async Task<PettyCashAccountResponse> UpdateAsync(
        Guid currentUserId,
        Guid accountId,
        UpdatePettyCashAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        var account = await _dbContext.PettyCashAccounts
            .Include(x => x.ConstructionSite)
            .SingleOrDefaultAsync(
                x => x.Id == accountId,
                cancellationToken)
            ?? throw new PettyCashManagementException(
                "Petty cash account not found.");

        if (IsSuperAdmin(actor))
        {
            // SuperAdmin may update any account.
        }
        else
        {
            if (account.CompanyId != actor.CompanyId)
            {
                throw new PettyCashManagementException(
                    "You are not authorized to access this petty cash account.");
            }

            if (HasRole(actor, AppRoles.SiteManager))
            {
                if (!account.ConstructionSiteId.HasValue)
                {
                    throw new PettyCashManagementException(
                        "You are not authorized to access this petty cash account.");
                }

                var hasSiteAccess = await _dbContext.UserSiteAccesses
                    .AnyAsync(
                        x =>
                            x.UserId == actor.Id &&
                            x.SiteId == account.ConstructionSiteId.Value &&
                            x.CompanyId == actor.CompanyId &&
                            x.IsActive,
                        cancellationToken);

                if (!hasSiteAccess)
                {
                    throw new PettyCashManagementException(
                        "You are not assigned to this site's petty cash scope.");
                }
            }
        }

        account.AccountName = request.AccountName;
        account.IsActive = request.IsActive;
        account.UpdatedBy = currentUserId;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(account);
    }

    private async Task<User> GetActorAsync(
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Users
            .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
            .SingleAsync(
                x => x.Id == currentUserId,
                cancellationToken);
    }

    private static bool IsSuperAdmin(User user)
    {
        return user.UserRoles.Any(
            x =>
                !x.IsDeleted &&
                !x.Role.IsDeleted &&
                string.Equals(
                    x.Role.Name,
                    "SuperAdmin",
                    StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasRole(User actor, string roleName)
    {
        return actor.UserRoles.Any(
            x => x.Role.Name == roleName);
    }

    private static PettyCashAccountResponse Map(
        PettyCashAccount account)
    {
        return new PettyCashAccountResponse
        {
            Id = account.Id,
            CompanyId = account.CompanyId,
            ConstructionSiteId = account.ConstructionSiteId,
            SiteName = account.ConstructionSite?.Name ?? string.Empty,
            AccountCode = account.AccountCode,
            AccountName = account.AccountName,
            OpeningBalance = account.OpeningBalance,
            CurrentBalance = account.CurrentBalance,
            IsActive = account.IsActive
        };
    }
}