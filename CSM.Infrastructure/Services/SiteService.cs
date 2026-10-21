using CSM.Application.Common.Exceptions;
using CSM.Application.Sites;
using CSM.Application.Sites.Dtos;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public sealed class SiteService : ISiteService
{
    private readonly ApplicationDbContext _dbContext;

    public SiteService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<SiteResponse>> GetAllAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        IQueryable<ConstructionSite> query =
            _dbContext.ConstructionSites
                .AsNoTracking();

        if (!IsSuperAdmin(actor))
        {
            EnsureSiteRole(actor);

            if (!actor.CompanyId.HasValue)
            {
                throw new SiteManagementException(
                    "Current user is not assigned to a company.");
            }

            if (HasRole(actor, AppRoles.CompanyAdmin) ||
                HasRole(actor, AppRoles.Accountant))
            {
                query = query.Where(
                    x => x.CompanyId == actor.CompanyId.Value);
            }
            else if (HasRole(actor, AppRoles.HrOfficer) ||
                     HasRole(actor, AppRoles.HROfficer))
            {
                query = query.Where(
                    site =>
                        site.CompanyId == actor.CompanyId.Value &&
                        _dbContext.SiteAssignments.Any(
                            assignment =>
                                assignment.ConstructionSiteId == site.Id &&
                                assignment.Employee.CompanyId == actor.CompanyId.Value &&
                                assignment.Employee.Status == EmployeeStatus.Active &&
                                (!assignment.EndDate.HasValue ||
                                 assignment.EndDate.Value >=
                                     DateOnly.FromDateTime(DateTime.UtcNow))));
            }
            else
            {
                query = query.Where(
                    x =>
                        x.CompanyId == actor.CompanyId.Value &&
                        _dbContext.UserSiteAccesses.Any(
                            access =>
                                access.UserId == currentUserId &&
                                access.SiteId == x.Id &&
                                access.CompanyId == actor.CompanyId.Value &&
                                access.IsActive));
            }
        }

        var sites = await query
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return sites
            .Select(Map)
            .ToArray();
    }

    public async Task<SiteResponse> GetByIdAsync(
        Guid currentUserId,
        Guid siteId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        var site = await GetSiteAsync(
            siteId,
            cancellationToken);

        EnsureSiteAccess(actor, site);

        return Map(site);
    }

    public async Task<SiteResponse> CreateAsync(
        Guid currentUserId,
        CreateSiteRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(currentUserId, cancellationToken);

        EnsureCanCreateOrModify(actor);

        // Resolve company from the logged-in user
        Guid companyId;

        if (IsSuperAdmin(actor))
        {
            if (request.CompanyId == Guid.Empty)
                throw new SiteManagementException("Company is required.");

            companyId = request.CompanyId;
        }
        else
        {
            if (actor.CompanyId is null)
                throw new SiteManagementException("Your account is not assigned to a company.");

            companyId = actor.CompanyId.Value;
        }

        ValidateSiteInput(
            request.SiteCode,
            request.Name,
            request.Latitude,
            request.Longitude,
            request.GeofenceRadiusMeters,
            request.PlannedStartDate,
            request.PlannedEndDate);

        var company = await _dbContext.Companies
            .SingleOrDefaultAsync(x => x.Id == companyId, cancellationToken);

        if (company is null)
            throw new SiteManagementException("Company was not found.");

        if (!company.IsActive)
            throw new SiteManagementException("Cannot create a site for an inactive company.");

        var normalizedSiteCode = request.SiteCode.Trim().ToUpperInvariant();

        var siteCodeExists = await _dbContext.ConstructionSites
            .IgnoreQueryFilters()
            .AnyAsync(x =>
                x.CompanyId == companyId &&
                x.SiteCode.ToUpper() == normalizedSiteCode,
                cancellationToken);

        if (siteCodeExists)
            throw new SiteManagementException("A site with this code already exists in the company.");

        await ValidateBusinessUnitAsync(
            companyId,
            request.BusinessUnitId,
            cancellationToken);

        var site = new ConstructionSite
        {
            CompanyId = companyId,
            SiteCode = normalizedSiteCode,
            Name = request.Name.Trim(),
            Description = Clean(request.Description),
            BusinessUnitId = request.BusinessUnitId,
            Address = Clean(request.Address),
            City = Clean(request.City),
            Region = Clean(request.Region),
            Country = Clean(request.Country),
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            GeofenceRadiusMeters = request.GeofenceRadiusMeters,
            PlannedStartDate = request.PlannedStartDate,
            PlannedEndDate = request.PlannedEndDate,
            Status = SiteStatus.Planning,
            IsActive = true
        };

        _dbContext.ConstructionSites.Add(site);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Map(site);
    }

    public async Task<SiteResponse> UpdateAsync(
        Guid currentUserId,
        Guid siteId,
        UpdateSiteRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanCreateOrModify(actor);

        var site = await GetSiteAsync(
            siteId,
            cancellationToken);

        EnsureSiteAccess(actor, site);

        ValidateSiteInput(
            site.SiteCode,
            request.Name,
            request.Latitude,
            request.Longitude,
            request.GeofenceRadiusMeters,
            request.PlannedStartDate,
            request.PlannedEndDate);

        await ValidateBusinessUnitAsync(
            site.CompanyId,
            request.BusinessUnitId,
            cancellationToken);

        site.Name = request.Name.Trim();
        site.Description = Clean(request.Description);
        site.BusinessUnitId = request.BusinessUnitId;
        site.Address = Clean(request.Address);
        site.City = Clean(request.City);
        site.Region = Clean(request.Region);
        site.Country = Clean(request.Country);
        site.Latitude = request.Latitude;
        site.Longitude = request.Longitude;
        site.GeofenceRadiusMeters =
            request.GeofenceRadiusMeters;
        site.PlannedStartDate =
            request.PlannedStartDate;
        site.PlannedEndDate =
            request.PlannedEndDate;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(site);
    }

    public async Task<SiteResponse> ChangeStatusAsync(
        Guid currentUserId,
        Guid siteId,
        ChangeSiteStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanCreateOrModify(actor);

        var site = await GetSiteAsync(
            siteId,
            cancellationToken);

        EnsureSiteAccess(actor, site);

        if (site.Status == request.Status)
        {
            return Map(site);
        }

        if (!IsValidTransition(
                site.Status,
                request.Status))
        {
            throw new SiteManagementException(
                $"Invalid site status transition from {site.Status} to {request.Status}.");
        }

        var today = DateOnly.FromDateTime(
            DateTime.UtcNow);

        if (request.Status == SiteStatus.Active &&
            !site.ActualStartDate.HasValue)
        {
            site.ActualStartDate = today;
        }

        if (request.Status == SiteStatus.Completed &&
            !site.ActualEndDate.HasValue)
        {
            site.ActualEndDate = today;
        }

        site.Status = request.Status;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(site);
    }

    public async Task SetActiveStatusAsync(
        Guid currentUserId,
        Guid siteId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanCreateOrModify(actor);

        var site = await GetSiteAsync(
            siteId,
            cancellationToken);

        EnsureSiteAccess(actor, site);

        site.IsActive = isActive;

        await _dbContext.SaveChangesAsync(
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<SiteResponse>> GetAssignmentOptionsAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        if (!IsSuperAdmin(actor))
        {
            if (!actor.CompanyId.HasValue)
            {
                throw new SiteManagementException(
                    "Current user is not assigned to a company.");
            }

            if (!HasRole(actor, AppRoles.CompanyAdmin) &&
                !HasRole(actor, AppRoles.HrOfficer) &&
                !HasRole(actor, AppRoles.HROfficer))
            {
                throw new SiteManagementException(
                    "You are not authorized to access site assignment options.");
            }
        }

        var query = _dbContext.ConstructionSites
            .AsNoTracking()
            .Where(x => x.IsActive);

        if (!IsSuperAdmin(actor))
        {
            query = query.Where(
                x => x.CompanyId == actor.CompanyId!.Value);
        }

        var sites = await query
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return sites
            .Select(Map)
            .ToArray();
    }

    private async Task<User> GetActorAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(
                x => x.Id == userId,
                cancellationToken);

        if (user is null || !user.IsActive)
        {
            throw new SiteManagementException(
                "Current user was not found or is inactive.");
        }

        return user;
    }

    private async Task<ConstructionSite> GetSiteAsync(
        Guid siteId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.ConstructionSites
            .SingleOrDefaultAsync(
                x => x.Id == siteId,
                cancellationToken)
            ?? throw new SiteManagementException(
                "Construction site was not found.");
    }

    private void EnsureSiteAccess(
        User actor,
        ConstructionSite site)
    {
        if (IsSuperAdmin(actor))
        {
            return;
        }

        EnsureSiteRole(actor);

        if (!actor.CompanyId.HasValue ||
            actor.CompanyId.Value != site.CompanyId)
        {
            throw new SiteManagementException(
                "You cannot access a site outside your company.");
        }

        if (HasRole(actor, AppRoles.CompanyAdmin))
        {
            return;
        }

        var hasSiteAccess = _dbContext.UserSiteAccesses.Any(
            access =>
                access.UserId == actor.Id &&
                access.SiteId == site.Id &&
                access.CompanyId == site.CompanyId &&
                access.IsActive);

        if (!hasSiteAccess)
        {
            throw new SiteManagementException(
                "You are not assigned to this site.");
        }
    }

    private static void EnsureCanCreateOrModify(
        User actor)
    {
        if (IsSuperAdmin(actor))
        {
            return;
        }

        if (HasRole(actor, AppRoles.CompanyAdmin) ||
            HasRole(actor, AppRoles.SiteManager))
        {
            return;
        }

        throw new SiteManagementException(
            "You are not authorized to create or modify construction sites.");
    }

    private static void EnsureSiteRole(User actor)
    {
        if (HasRole(actor, AppRoles.CompanyAdmin) ||
            HasRole(actor, AppRoles.Accountant) ||
            HasRole(actor, AppRoles.SiteManager) ||
            HasRole(actor, AppRoles.Supervisor) ||
            HasRole(actor, AppRoles.Storekeeper) ||
            HasRole(actor, AppRoles.ProcurementOfficer) ||
            HasRole(actor, AppRoles.HrOfficer) ||
            HasRole(actor, AppRoles.HROfficer))
        {
            return;
        }

        throw new SiteManagementException(
            "You are not authorized to access construction sites.");
    }

    private static bool IsSuperAdmin(User user)
    {
        return HasRole(user, AppRoles.SuperAdmin);
    }

    private static bool HasRole(
        User user,
        string roleName)
    {
        return user.UserRoles.Any(
            x =>
                !x.IsDeleted &&
                !x.Role.IsDeleted &&
                string.Equals(
                    x.Role.Name,
                    roleName,
                    StringComparison.OrdinalIgnoreCase));
    }

    private async Task ValidateBusinessUnitAsync(
        Guid companyId,
        Guid? businessUnitId,
        CancellationToken cancellationToken)
    {
        if (!businessUnitId.HasValue)
        {
            return;
        }

        var valid = await _dbContext.BusinessUnits
            .AnyAsync(
                x =>
                    x.Id == businessUnitId.Value &&
                    x.CompanyId == companyId,
                cancellationToken);

        if (!valid)
        {
            throw new SiteManagementException(
                "The selected business unit does not belong to this company.");
        }
    }

    private static void ValidateSiteInput(
        string siteCode,
        string name,
        decimal? latitude,
        decimal? longitude,
        decimal? geofenceRadiusMeters,
        DateOnly? plannedStartDate,
        DateOnly? plannedEndDate)
    {
        if (string.IsNullOrWhiteSpace(siteCode))
        {
            throw new SiteManagementException(
                "Site code is required.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new SiteManagementException(
                "Site name is required.");
        }

        if (latitude.HasValue &&
            (latitude.Value < -90 ||
             latitude.Value > 90))
        {
            throw new SiteManagementException(
                "Latitude must be between -90 and 90.");
        }

        if (longitude.HasValue &&
            (longitude.Value < -180 ||
             longitude.Value > 180))
        {
            throw new SiteManagementException(
                "Longitude must be between -180 and 180.");
        }

        if (latitude.HasValue != longitude.HasValue)
        {
            throw new SiteManagementException(
                "Latitude and longitude must be provided together.");
        }

        if (geofenceRadiusMeters.HasValue &&
            geofenceRadiusMeters.Value <= 0)
        {
            throw new SiteManagementException(
                "Geofence radius must be greater than zero.");
        }

        if (geofenceRadiusMeters.HasValue &&
            (!latitude.HasValue || !longitude.HasValue))
        {
            throw new SiteManagementException(
                "A geofence requires latitude and longitude.");
        }

        if (plannedStartDate.HasValue &&
            plannedEndDate.HasValue &&
            plannedEndDate.Value <
            plannedStartDate.Value)
        {
            throw new SiteManagementException(
                "Planned end date cannot be earlier than planned start date.");
        }
    }

    private static bool IsValidTransition(
        SiteStatus current,
        SiteStatus next)
    {
        return current switch
        {
            SiteStatus.Planning =>
                next is SiteStatus.Active
                    or SiteStatus.OnHold,

            SiteStatus.Active =>
                next is SiteStatus.OnHold
                    or SiteStatus.Completed,

            SiteStatus.OnHold =>
                next is SiteStatus.Active
                    or SiteStatus.Completed,

            SiteStatus.Completed =>
                next == SiteStatus.Closed,

            SiteStatus.Closed => false,

            _ => false
        };
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static SiteResponse Map(
        ConstructionSite site)
    {
        return new SiteResponse(
            site.Id,
            site.CompanyId,
            site.SiteCode,
            site.Name,
            site.Description,
            site.BusinessUnitId,
            site.Address,
            site.City,
            site.Region,
            site.Country,
            site.Latitude,
            site.Longitude,
            site.GeofenceRadiusMeters,
            site.PlannedStartDate,
            site.PlannedEndDate,
            site.ActualStartDate,
            site.ActualEndDate,
            site.Status,
            site.IsActive,
            site.CreatedAtUtc);
    }
}