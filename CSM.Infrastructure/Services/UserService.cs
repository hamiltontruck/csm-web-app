using CSM.Application.Common.Exceptions;
using CSM.Application.Common.Security;
using CSM.Application.Users;
using CSM.Application.Users.Dtos;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public sealed class UserService : IUserService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;

    public UserService(
        ApplicationDbContext dbContext,
        IPasswordHasher passwordHasher)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
    }

    public async Task<IReadOnlyCollection<UserResponse>> GetAllAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(currentUserId, cancellationToken);
        var query = _dbContext.Users.AsNoTracking().Where(x => !x.IsDeleted);
        if (!IsSuperAdmin(actor))
            query = query.Where(x => x.CompanyId == actor.CompanyId);

        var users = await query
            .Include(x => x.UserRoles).ThenInclude(x => x.Role)
            .Include(x => x.SiteAccesses).ThenInclude(x => x.Site)
            .OrderBy(x => x.FirstName).ThenBy(x => x.LastName)
            .ToListAsync(cancellationToken);
        return users.Select(Map).ToList();
    }

    public async Task<UserResponse> GetByIdAsync(
        Guid currentUserId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        var target = await GetUserAsync(
            userId,
            cancellationToken);

        EnsureCanManage(actor, target);

        return Map(target);
    }

    public async Task<UserResponse> CreateAsync(
        Guid currentUserId,
        CreateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        ValidateBasicInput(request);
        ValidatePassword(request.TemporaryPassword);

        var requestedRoles = NormalizeRoles(request.Roles);

        if (requestedRoles.Count == 0)
        {
            throw new UserManagementException(
                "At least one role is required.");
        }

        var actorIsSuperAdmin = IsSuperAdmin(actor);

        if (!actorIsSuperAdmin)
        {
            EnsureCompanyAdmin(actor);

            if (!actor.CompanyId.HasValue)
            {
                throw new UserManagementException(
                    "Company administrator is not assigned to a company.");
            }

            if (request.CompanyId != actor.CompanyId)
            {
                throw new UserManagementException(
                    "You can only create users in your own company.");
            }

            if (requestedRoles.Contains(
                    AppRoles.SuperAdmin,
                    StringComparer.OrdinalIgnoreCase))
            {
                throw new UserManagementException(
                    "Company administrators cannot assign the SuperAdmin role.");
            }
        }

        if (requestedRoles.Contains(
                AppRoles.SuperAdmin,
                StringComparer.OrdinalIgnoreCase) &&
            request.CompanyId.HasValue)
        {
            throw new UserManagementException(
                "SuperAdmin users must not belong to a company.");
        }

        if (!requestedRoles.Contains(
                AppRoles.SuperAdmin,
                StringComparer.OrdinalIgnoreCase) &&
            !request.CompanyId.HasValue)
        {
            throw new UserManagementException(
                "Non-SuperAdmin users must belong to a company.");
        }


        if (request.CompanyId.HasValue &&
            !await _dbContext.Companies.AnyAsync(
                x => x.Id == request.CompanyId.Value && x.IsActive && !x.IsDeleted,
                cancellationToken))
        {
            throw new UserManagementException("Select an active company before creating a user.");
        }

        var siteIds = (request.SiteIds ?? []).Distinct().ToList();
        if (siteIds.Count > 0 && !request.CompanyId.HasValue)
            throw new UserManagementException("Site assignments require a company.");

        if (request.DefaultSiteId.HasValue && !siteIds.Contains(request.DefaultSiteId.Value))
            throw new UserManagementException("The default site must be one of the assigned sites.");

        var assignedSites = await _dbContext.ConstructionSites
            .Where(x => siteIds.Contains(x.Id) && x.CompanyId == request.CompanyId &&
                        x.IsActive && !x.IsDeleted)
            .ToListAsync(cancellationToken);
        if (assignedSites.Count != siteIds.Count)
            throw new UserManagementException("Selected sites must be active and belong to the selected company.");

        var normalizedEmail =
            NormalizeEmail(request.Email);

        var emailExists = await _dbContext.Users
            .IgnoreQueryFilters()
            .AnyAsync(
                x => x.NormalizedEmail == normalizedEmail,
                cancellationToken);

        if (emailExists)
        {
            throw new UserManagementException(
                "A user with this email already exists.");
        }

        var roles = await ResolveRolesAsync(
            requestedRoles,
            cancellationToken);

        if (request.EmployeeId.HasValue)
        {
            var employeeExists =
                await _dbContext.Employees.AnyAsync(
                    x => x.Id == request.EmployeeId.Value && x.CompanyId == request.CompanyId,
                    cancellationToken);

            if (!employeeExists)
            {
                throw new UserManagementException(
                    "The selected employee must belong to the selected company.");
            }
        }

        var user = new User
        {
            CompanyId = request.CompanyId,
            EmployeeId = request.EmployeeId,
            Email = request.Email.Trim(),
            NormalizedEmail = normalizedEmail,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            PasswordHash =
                _passwordHasher.Hash(
                    request.TemporaryPassword),
            IsActive = true,
            PhoneNumber = request.PhoneNumber?.Trim() ?? string.Empty,
            JobTitle = request.JobTitle?.Trim() ?? string.Empty,
            DefaultSiteId = request.DefaultSiteId,
            MustChangePassword = true
        };

        foreach (var role in roles)
        {
            user.UserRoles.Add(
                new UserRole
                {
                    RoleId = role.Id
                });
        }

        foreach (var site in assignedSites)
        {
            user.SiteAccesses.Add(new UserSiteAccess
            {
                CompanyId = site.CompanyId,
                SiteId = site.Id,
                Site = site,
                IsActive = true,
                IsDefault = request.DefaultSiteId == site.Id
            });
        }

        _dbContext.Users.Add(user);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        await WriteAuditAsync(
            currentUserId,
            user.Id,
            "Create User",
            $"Created {user.Email}",
            cancellationToken);

        return Map(user);
    }

    public async Task<UserResponse> UpdateAsync(
        Guid currentUserId,
        Guid userId,
        UpdateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(currentUserId, cancellationToken);

        var target = await GetUserAsync(userId, cancellationToken);

        EnsureCanManage(actor, target);

        if (string.IsNullOrWhiteSpace(request.FirstName) ||
            string.IsNullOrWhiteSpace(request.LastName))
        {
            throw new UserManagementException(
                "First name and last name are required.");
        }

        if (request.EmployeeId.HasValue)
        {
            var employeeExists = await _dbContext.Employees.AnyAsync(
                x => x.Id == request.EmployeeId.Value,
                cancellationToken);

            if (!employeeExists)
            {
                throw new UserManagementException(
                    "The selected employee does not exist.");
            }
        }


        // Basic information only
        target.FirstName = request.FirstName.Trim();
        target.LastName = request.LastName.Trim();
        target.EmployeeId = request.EmployeeId;

        target.PhoneNumber =
            string.IsNullOrWhiteSpace(request.PhoneNumber)
                ? string.Empty
                : request.PhoneNumber.Trim();

        target.JobTitle =
            string.IsNullOrWhiteSpace(request.JobTitle)
                ? string.Empty
                : request.JobTitle.Trim();

        target.IsActive = request.IsActive;



        await _dbContext.SaveChangesAsync(cancellationToken);

        await WriteAuditAsync(
            currentUserId,
            target.Id,
            "Update User",
            "Updated profile information",
            cancellationToken);

        return await GetByIdAsync(
            currentUserId,
            userId,
            cancellationToken);
    }

   public async Task SetRolesAsync(
       Guid currentUserId,
       Guid userId,
       SetUserRolesRequest request,
       CancellationToken cancellationToken)
   {
        var actor = await GetActorAsync(currentUserId, cancellationToken);
        var target = await GetUserAsync(userId, cancellationToken);
        EnsureCanManage(actor, target);

        var requestedNames = NormalizeRoles(request.Roles);
        if (requestedNames.Count == 0)
            throw new UserManagementException("At least one role is required.");
        var grantsSuperAdmin = requestedNames.Contains(AppRoles.SuperAdmin, StringComparer.OrdinalIgnoreCase);
        if (grantsSuperAdmin && !IsSuperAdmin(actor))
            throw new UserManagementException("Company administrators cannot assign the SuperAdmin role.");
        if (grantsSuperAdmin && target.CompanyId.HasValue)
            throw new UserManagementException("SuperAdmin users must not belong to a company.");
        if (!grantsSuperAdmin && !target.CompanyId.HasValue)
            throw new UserManagementException("Non-SuperAdmin users must belong to a company.");

        var requestedRoles = await ResolveRolesAsync(requestedNames, cancellationToken);
        var requestedRoleIds = requestedRoles.Select(x => x.Id).ToList();

        var executionStrategy = _dbContext.Database.CreateExecutionStrategy();

        await executionStrategy.ExecuteAsync(
            async () =>
            {
                await using var transaction =
                    await _dbContext.Database.BeginTransactionAsync(
                        System.Data.IsolationLevel.Serializable,
                        cancellationToken);

                var existingLinks = await _dbContext.UserRoles
                    .IgnoreQueryFilters()
                    .Where(x => x.UserId == userId)
                    .ToListAsync(cancellationToken);

                var existingRoleIds = existingLinks
                    .Where(x => !x.IsDeleted)
                    .Select(x => x.RoleId)
                    .ToHashSet();

                var linksToRemove = existingLinks
                    .Where(x => !x.IsDeleted && !requestedRoleIds.Contains(x.RoleId))
                    .ToList();

                if (linksToRemove.Count > 0)
                {
                    _dbContext.UserRoles.RemoveRange(linksToRemove);
                }

                foreach (var roleId in requestedRoleIds)
                {
                    var existingLink = existingLinks
                        .SingleOrDefault(x => x.RoleId == roleId);

                    if (existingLink?.IsDeleted == true)
                    {
                        existingLink.IsDeleted = false;
                        existingLink.DeletedAtUtc = null;
                        existingLink.DeletedBy = null;
                    }
                    else if (!existingRoleIds.Contains(roleId))
                    {
                        _dbContext.UserRoles.Add(new UserRole
                        {
                            Id = Guid.NewGuid(),
                            UserId = userId,
                            RoleId = roleId
                        });
                    }
                }

                await _dbContext.SaveChangesAsync(cancellationToken);

                await WriteAuditAsync(
                    currentUserId,
                    userId,
                    "Update Roles",
                    string.Join(", ", request.Roles),
                    cancellationToken);

                await transaction.CommitAsync(cancellationToken);
            });
   }
    public async Task SetActiveStatusAsync(
        Guid currentUserId,
        Guid userId,
        bool isActive,
        CancellationToken cancellationToken)
    {
        var currentUser = await _dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == currentUserId,
                cancellationToken)
            ?? throw new UserManagementException("Current user not found.");

        var user = await _dbContext.Users
            .SingleOrDefaultAsync(
                x => x.Id == userId,
                cancellationToken)
            ?? throw new UserManagementException("User not found.");

        if (currentUser.CompanyId != null &&
            user.CompanyId != currentUser.CompanyId)
        {
            throw new UserManagementException(
                "You can only manage users in your own company.");
        }

        user.IsActive = isActive;
        user.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<User> GetActorAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(
            userId,
            cancellationToken);

        if (!user.IsActive)
        {
            throw new UserManagementException(
                "Current user account is inactive.");
        }

        return user;
    }

    private async Task<User> GetUserAsync(
    Guid userId,
    CancellationToken cancellationToken)
{
    return await _dbContext.Users
        .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
        .Include(x => x.SiteAccesses)
            .ThenInclude(x => x.Site)
        .SingleOrDefaultAsync(
            x => x.Id == userId,
            cancellationToken)
        ?? throw new UserManagementException(
            "User was not found.");
}

    private void EnsureCanManage(
        User actor,
        User target)
    {
        if (IsSuperAdmin(actor))
        {
            return;
        }

        EnsureCompanyAdmin(actor);

        if (!actor.CompanyId.HasValue ||
            actor.CompanyId != target.CompanyId)
        {
            throw new UserManagementException(
                "You cannot manage users outside your company.");
        }

        if (IsSuperAdmin(target))
        {
            throw new UserManagementException(
                "Company administrators cannot manage SuperAdmin users.");
        }
    }

    private static void EnsureCompanyAdmin(User user)
    {
        if (!HasRole(user, AppRoles.CompanyAdmin))
        {
            throw new UserManagementException(
                "You are not authorized to manage users.");
        }
    }

    private static bool IsSuperAdmin(User user) =>
        HasRole(user, AppRoles.SuperAdmin);

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

    private async Task<List<Role>> ResolveRolesAsync(
        IReadOnlyCollection<string> roleNames,
        CancellationToken cancellationToken)
    {
        var normalized =
            roleNames
                .Select(x => x.ToUpperInvariant())
                .ToArray();

        var roles = await _dbContext.Roles
            .Where(x =>
                normalized.Contains(x.NormalizedName))
            .ToListAsync(cancellationToken);

        if (roles.Count != normalized.Length)
        {
            throw new UserManagementException(
                "One or more selected roles are invalid.");
        }

        return roles;
    }

    public async Task SetSitesAsync(
        Guid currentUserId,
        Guid userId,
        SetUserSitesRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(currentUserId, cancellationToken);
        var target = await GetUserAsync(userId, cancellationToken);

        EnsureCanManage(actor, target);

        var sites = await ValidateSiteSelectionAsync(target, request.DefaultSiteId, request.SiteIds, cancellationToken);
        var selectedIds = sites.Select(x => x.Id).ToHashSet();
        var existing = await _dbContext.UserSiteAccesses.IgnoreQueryFilters()
            .Where(x => x.UserId == target.Id).ToListAsync(cancellationToken);

        foreach (var access in existing)
        {
            if (access.CompanyId == target.CompanyId && selectedIds.Contains(access.SiteId))
            {
                access.IsDeleted = false;
                access.DeletedAtUtc = null;
                access.DeletedBy = null;
                access.IsActive = true;
                access.IsDefault = request.DefaultSiteId == access.SiteId;
            }
            else if (!access.IsDeleted)
            {
                access.IsActive = false;
                access.IsDefault = false;
                _dbContext.UserSiteAccesses.Remove(access);
            }
        }

        foreach (var site in sites)
        {
            if (existing.Any(x => x.SiteId == site.Id && x.CompanyId == target.CompanyId))
                continue;
            _dbContext.UserSiteAccesses.Add(new UserSiteAccess
            {
                Id = Guid.NewGuid(),
                CompanyId = site.CompanyId,
                UserId = target.Id,
                SiteId = site.Id,
                Site = site,
                IsActive = true,
                IsDefault = request.DefaultSiteId == site.Id
            });
        }

        // Persist the default together with its assignments in the same SaveChanges transaction.
        target.DefaultSiteId = request.DefaultSiteId;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await WriteAuditAsync(
            currentUserId,
            userId,
            "Update Sites",
            $"{request.SiteIds.Count} assigned site(s)",
            cancellationToken);
    }

    private async Task<List<ConstructionSite>> ValidateSiteSelectionAsync(
        User target, Guid? defaultSiteId, IReadOnlyCollection<Guid>? siteIds, CancellationToken cancellationToken)
    {
        var uniqueIds = (siteIds ?? []).Distinct().ToArray();
        if (uniqueIds.Length > 0 && !target.CompanyId.HasValue)
            throw new UserManagementException("Site assignments require a user company.");
        if (defaultSiteId.HasValue && !uniqueIds.Contains(defaultSiteId.Value))
            throw new UserManagementException("The default site must be one of the assigned sites.");
        var sites = await _dbContext.ConstructionSites
            .Where(x => uniqueIds.Contains(x.Id) && x.CompanyId == target.CompanyId && x.IsActive)
            .ToListAsync(cancellationToken);
        if (sites.Count != uniqueIds.Length)
            throw new UserManagementException("Selected sites must be active and belong to the user's company.");
        return sites;
    }

    public async Task ActivateAsync(
        Guid currentUserId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(currentUserId, cancellationToken);
        var target = await GetUserAsync(userId, cancellationToken);

        EnsureCanManage(actor, target);

        target.IsActive = true;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await WriteAuditAsync(
            currentUserId,
            target.Id,
            "Activate User",
            "",
            cancellationToken);
    }

    public async Task DeactivateAsync(
        Guid currentUserId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(currentUserId, cancellationToken);
        var target = await GetUserAsync(userId, cancellationToken);

        EnsureCanManage(actor, target);

        target.IsActive = false;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await WriteAuditAsync(
            currentUserId,
            target.Id,
            "Deactivate User",
            "",
            cancellationToken);
    }

    public async Task DeleteAsync(
        Guid currentUserId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(currentUserId, cancellationToken);
        var target = await GetUserAsync(userId, cancellationToken);

        EnsureCanManage(actor, target);

        // Prevent deleting yourself
        if (actor.Id == target.Id)
            throw new UserManagementException(
                "You cannot delete your own account.");

        target.IsDeleted = true;
        target.IsActive = false;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await WriteAuditAsync(
            currentUserId,
            target.Id,
            "Delete User",
            "Soft deleted",
            cancellationToken);
    }

    public async Task<UserProfileResponse> GetProfileAsync(
        Guid currentUserId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(currentUserId, cancellationToken);

        var user = await _dbContext.Users
            .AsNoTracking()
            .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
            .Include(x => x.SiteAccesses)
                .ThenInclude(x => x.Site)
            .SingleAsync(x => x.Id == userId, cancellationToken);

        return new UserProfileResponse(
            user.Id,
            user.EmployeeId?.ToString() ?? "",
            user.FirstName,
            user.LastName,
            user.Email,
            user.PhoneNumber,
            user.JobTitle,
            user.IsActive,
            user.DefaultSiteId,
            user.SiteAccesses
                .FirstOrDefault(x => x.SiteId == user.DefaultSiteId)?
                .Site.Name,
            user.CreatedAtUtc,
            user.LastLoginAtUtc,
            user.UserRoles
                .Select(x => x.Role.Name)
                .ToArray(),
            user.SiteAccesses
                .Where(x => x.IsActive)
                .Select(x => new SiteSummaryDto(
                    x.SiteId,
                    x.Site.Name,
                    x.SiteId == user.DefaultSiteId))
                .ToArray()
        );
    }

    private async Task WriteAuditAsync(
        Guid actorUserId,
        Guid targetUserId,
        string action,
        string details,
        CancellationToken cancellationToken)
    {
        _dbContext.UserAudits.Add(new UserAudit
        {
            ActorUserId = actorUserId,
            TargetUserId = targetUserId,
            Action = action,
            Details = details,
            PerformedAtUtc = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<UserAuditResponse>> GetAuditAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(currentUserId, cancellationToken);

        return await _dbContext.UserAudits
            .AsNoTracking()
            .Include(x => x.ActorUser)
            .Include(x => x.TargetUser)
            .OrderByDescending(x => x.PerformedAtUtc)
            .Select(x => new UserAuditResponse(
                x.Id,
                x.PerformedAtUtc,
                x.ActorUser.FirstName + " " + x.ActorUser.LastName,
                x.Action,
                x.TargetUser.FirstName + " " + x.TargetUser.LastName,
                x.Details))
            .ToListAsync(cancellationToken);
    }

    private static List<string> NormalizeRoles(
        IReadOnlyCollection<string>? roles)
    {
        if (roles is null)
        {
            return [];
        }

        return roles
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new UserManagementException(
                "Email is required.");
        }

        return email.Trim().ToUpperInvariant();
    }

    private static void ValidateBasicInput(
        CreateUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName))
        {
            throw new UserManagementException(
                "First name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.LastName))
        {
            throw new UserManagementException(
                "Last name is required.");
        }
    }

    private static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) ||
            password.Length < 12 ||
            !password.Any(char.IsUpper) ||
            !password.Any(char.IsLower) ||
            !password.Any(char.IsDigit) ||
            !password.Any(
                x => !char.IsLetterOrDigit(x)))
        {
            throw new UserManagementException(
                "Temporary password must be at least 12 characters and contain uppercase, lowercase, number, and special characters.");
        }
    }

    private static UserResponse Map(User user)
    {
        var roles = user.UserRoles
            .Where(x => !x.IsDeleted && !x.Role.IsDeleted)
            .Select(x => x.Role.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToArray();

        var siteIds = user.SiteAccesses?
            .Where(x => x.IsActive && !x.IsDeleted)
            .Select(x => x.SiteId)
            .Distinct()
            .ToArray()
            ?? Array.Empty<Guid>();

        var siteNames = user.SiteAccesses?
            .Where(x => x.IsActive && !x.IsDeleted)
            .Select(x => x.Site.Name)
            .Distinct()
            .OrderBy(x => x)
            .ToArray()
            ?? Array.Empty<string>();

        return new UserResponse(
            user.Id,
            user.CompanyId,
            user.EmployeeId,
            user.Email,
            user.FirstName,
            user.LastName,
            $"{user.FirstName} {user.LastName}",
            user.PhoneNumber,
            user.JobTitle,
            user.IsActive,
            user.MustChangePassword,
            user.DefaultSiteId,
            user.LastLoginAtUtc,
            roles,
            siteIds,
            siteNames,
            user.CreatedAtUtc
        );
    }
}