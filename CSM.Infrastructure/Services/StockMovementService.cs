using CSM.Application.Common.Exceptions;
using CSM.Application.Inventory.StockMovements;
using CSM.Application.Inventory.StockMovements.Dtos;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Procurement;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public sealed class StockMovementService : IStockMovementService
{
    private readonly ApplicationDbContext _dbContext;

    public StockMovementService(
        ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<StockMovementResponse>> GetAllAsync(
        Guid currentUserId,
        Guid? constructionSiteId = null,
        Guid? materialId = null,
        StockMovementType? movementType = null,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanManage(actor);

        ValidateDateRange(
            fromUtc,
            toUtc);

        var query = BuildQuery();

        if (!IsSuperAdmin(actor))
        {
            var companyId =
                GetActorCompanyId(actor);

            query = query.Where(
                x => x.CompanyId == companyId);
        }

        if (constructionSiteId.HasValue)
        {
            query = query.Where(
                x =>
                    x.StockItem.ConstructionSiteId ==
                    constructionSiteId.Value);
        }

        if (materialId.HasValue)
        {
            query = query.Where(
                x =>
                    x.StockItem.MaterialId ==
                    materialId.Value);
        }

        if (movementType.HasValue)
        {
            query = query.Where(
                x =>
                    x.MovementType ==
                    movementType.Value);
        }

        if (fromUtc.HasValue)
        {
            query = query.Where(
                x =>
                    x.MovementDateUtc >=
                    fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(
                x =>
                    x.MovementDateUtc <=
                    toUtc.Value);
        }

        var records = await query
            .OrderByDescending(
                x => x.MovementDateUtc)
            .ThenByDescending(
                x => x.CreatedAtUtc)
            .ToListAsync(
                cancellationToken);

        return records
            .Select(record => Map(record))
            .ToArray();
    }

    public async Task<StockMovementResponse> GetByIdAsync(
        Guid currentUserId,
        Guid stockMovementId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanManage(actor);

        var record = await BuildQuery()
            .SingleOrDefaultAsync(
                x => x.Id == stockMovementId,
                cancellationToken)
            ?? throw new StockMovementManagementException(
                "Stock movement was not found.");

        EnsureCompanyAccess(
            actor,
            record.CompanyId);

        string? moveFrom = null;
        string? moveTo = null;

        if (record.ReferenceType == "StockTransfer")
        {
            if (record.MovementType == StockMovementType.TransferOut)
            {
                moveFrom = record.StockItem.ConstructionSite.Name;

                moveTo = await _dbContext.StockMovements
                    .AsNoTracking()
                    .Include(x => x.StockItem)
                        .ThenInclude(x => x.ConstructionSite)
                    .Where(x => x.ReferenceId == record.Id)
                    .Select(x => x.StockItem.ConstructionSite.Name)
                    .FirstOrDefaultAsync(cancellationToken);
            }
            else if (record.MovementType == StockMovementType.TransferIn &&
                     record.ReferenceId.HasValue)
            {
                var source = await _dbContext.StockMovements
                    .AsNoTracking()
                    .Include(x => x.StockItem)
                        .ThenInclude(x => x.ConstructionSite)
                    .FirstOrDefaultAsync(
                        x => x.Id == record.ReferenceId.Value,
                        cancellationToken);

                moveFrom = source?.StockItem.ConstructionSite.Name;
                moveTo = record.StockItem.ConstructionSite.Name;
            }
        }

        return Map(record, moveFrom, moveTo);
    }

    private IQueryable<StockMovement> BuildQuery()
    {
        return _dbContext.StockMovements
            .AsNoTracking()
            .Include(x => x.StockItem)
                .ThenInclude(x => x.ConstructionSite)
            .Include(x => x.StockItem)
                .ThenInclude(x => x.Material);
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
            ?? throw new StockMovementManagementException(
                "Authenticated user was not found.");
    }

    private static void EnsureCanManage(
        User actor)
    {
        if (IsSuperAdmin(actor) ||
            HasRole(actor, AppRoles.Storekeeper) ||
            HasRole(actor, AppRoles.ProcurementOfficer))
        {
            return;
        }

        throw new StockMovementManagementException(
            "You are not authorized to access stock movements.");
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
            throw new StockMovementManagementException(
                "You are not authorized to access this stock movement.");
        }
    }

    private static Guid GetActorCompanyId(
        User actor)
    {
        if (!actor.CompanyId.HasValue)
        {
            throw new StockMovementManagementException(
                "Current user is not assigned to a company.");
        }

        return actor.CompanyId.Value;
    }

    private static bool IsSuperAdmin(
        User actor)
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

    private static void ValidateDateRange(
        DateTime? fromUtc,
        DateTime? toUtc)
    {
        if (fromUtc.HasValue &&
            toUtc.HasValue &&
            fromUtc.Value > toUtc.Value)
        {
            throw new StockMovementManagementException(
                "'fromUtc' cannot be later than 'toUtc'.");
        }
    }

    private static StockMovementResponse Map(
        StockMovement record,
        string? moveFrom = null,
        string? moveTo = null)
    {
        return new StockMovementResponse
        {
            Id = record.Id,

            StockItemId =
                record.StockItemId,

            ConstructionSiteId =
                record.StockItem.ConstructionSiteId,

            SiteName =
                record.StockItem.ConstructionSite.Name,

                MoveFrom = moveFrom,
                MoveTo = moveTo,

            MaterialId =
                record.StockItem.MaterialId,

            MaterialCode =
                record.StockItem.Material.MaterialCode,

            MaterialName =
                record.StockItem.Material.Name,

            UnitOfMeasure =
                record.StockItem.Material.UnitOfMeasure,

            MovementType =
                record.MovementType,

            MovementDateUtc =
                record.MovementDateUtc,

            Quantity =
                record.Quantity,

            StockBalanceBefore =
                record.StockBalanceBefore,

            StockBalanceAfter =
                record.StockBalanceAfter,

            UnitCost =
                record.UnitCost,

            TotalCost =
                record.UnitCost.HasValue
                    ? record.Quantity *
                      record.UnitCost.Value
                    : null,

            ReferenceType =
                record.ReferenceType,

            ReferenceId =
                record.ReferenceId,

            ReferenceNumber =
                record.ReferenceNumber,

            Remarks =
                record.Remarks
        };
    }
}

