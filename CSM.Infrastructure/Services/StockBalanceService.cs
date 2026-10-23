using CSM.Application.Common.Exceptions;
using CSM.Application.Inventory.StockBalances;
using CSM.Application.Inventory.StockBalances.Dtos;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Procurement;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public sealed class StockBalanceService : IStockBalanceService
{
    private readonly ApplicationDbContext _dbContext;

    public StockBalanceService(
        ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<StockBalanceResponse>> GetAllAsync(
        Guid currentUserId,
        Guid? constructionSiteId = null,
        Guid? materialId = null,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanRead(actor);

        var query = BuildQuery();

        if (!IsSuperAdmin(actor))
        {
            var companyId = GetActorCompanyId(actor);

            query = query.Where(
                x => x.CompanyId == companyId);
        }

        if (IsSiteScopedSupervisor(actor))
        {
            var allowedSites = GetSupervisorSiteIds(actor);
            query = query.Where(x => allowedSites.Contains(x.ConstructionSiteId));
        }

        if (constructionSiteId.HasValue)
        {
            query = query.Where(
                x => x.ConstructionSiteId ==
                    constructionSiteId.Value);
        }

        if (materialId.HasValue)
        {
            query = query.Where(
                x => x.MaterialId ==
                    materialId.Value);
        }

        var records = await query
            .OrderBy(x => x.ConstructionSite.Name)
            .ThenBy(x => x.Material.Name)
            .ToListAsync(cancellationToken);

        return records
            .Select(Map)
            .ToArray();
    }

    public async Task<StockBalanceResponse> GetByIdAsync(
        Guid currentUserId,
        Guid stockItemId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanRead(actor);

        var query = BuildQuery();

        if (IsSiteScopedSupervisor(actor))
        {
            var allowedSites = GetSupervisorSiteIds(actor);
            query = query.Where(x => allowedSites.Contains(x.ConstructionSiteId));
        }

        var record = await query
            .SingleOrDefaultAsync(
                x => x.Id == stockItemId,
                cancellationToken)
            ?? throw new StockBalanceManagementException(
                "Stock balance was not found.");

        EnsureCompanyAccess(
            actor,
            record.CompanyId);

        return Map(record);
    }

    private static bool IsSiteScopedSupervisor(User actor) =>
        HasRole(actor, AppRoles.Supervisor) &&
        !IsSuperAdmin(actor) &&
        !HasRole(actor, AppRoles.Storekeeper) &&
        !HasRole(actor, AppRoles.ProcurementOfficer);

    private IQueryable<Guid> GetSupervisorSiteIds(User actor)
    {
        var companyId = GetActorCompanyId(actor);
        return _dbContext.UserSiteAccesses
            .Where(x => x.UserId == actor.Id && x.CompanyId == companyId &&
                x.IsActive && x.Site.IsActive && x.Site.CompanyId == companyId)
            .Select(x => x.SiteId);
    }

    private IQueryable<StockItem> BuildQuery()
    {
        return _dbContext.StockItems
            .AsNoTracking()
            .Include(x => x.ConstructionSite)
            .Include(x => x.Material);
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
            ?? throw new StockBalanceManagementException(
                "Authenticated user was not found.");
    }

    private static void EnsureCanRead(User actor)
    {
        if (!actor.IsActive)
            throw new StockBalanceManagementException("You are not authorized to access stock balances.");

        if (IsSuperAdmin(actor) ||
            HasRole(actor, AppRoles.Supervisor) ||
            HasRole(actor, AppRoles.Storekeeper) ||
            HasRole(actor, AppRoles.ProcurementOfficer))
        {
            return;
        }

        throw new StockBalanceManagementException(
            "You are not authorized to access stock balances.");
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
            throw new StockBalanceManagementException(
                "You are not authorized to access this stock balance.");
        }
    }

    private static Guid GetActorCompanyId(User actor)
    {
        if (!actor.CompanyId.HasValue)
        {
            throw new StockBalanceManagementException(
                "Current user is not assigned to a company.");
        }

        return actor.CompanyId.Value;
    }

    private static bool IsSuperAdmin(User actor)
    {
        return HasRole(actor, AppRoles.SuperAdmin) ||
            HasRole(actor, AppRoles.Administrator);
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

    private static StockBalanceResponse Map(
        StockItem record)
    {
        return new StockBalanceResponse
        {
            StockItemId =
                record.Id,

            ConstructionSiteId =
                record.ConstructionSiteId,

            SiteName =
                record.ConstructionSite.Name,

            MaterialId =
                record.MaterialId,

            MaterialCode =
                record.Material.MaterialCode,

            MaterialName =
                record.Material.Name,

            UnitOfMeasure =
                record.Material.UnitOfMeasure,

            QuantityOnHand =
                record.QuantityOnHand,

            QuantityReserved =
                record.QuantityReserved,

            AvailableQuantity =
                record.QuantityOnHand -
                record.QuantityReserved,

            ReorderLevel =
                record.ReorderLevel,

            MaximumStockLevel =
                record.MaximumStockLevel,

            AverageUnitCost =
                record.AverageUnitCost,

            StorageLocation =
                record.StorageLocation
        };
    }
}
