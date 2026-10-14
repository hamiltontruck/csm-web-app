using CSM.Domain.Enums;
using CSM.Domain.Entities.Procurement;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Identity;
using CSM.Application.Common.Security;
using Microsoft.EntityFrameworkCore;
using CSM.Application.Common.Exceptions;
using CSM.Application.Inventory.StockAdjustments;
using CSM.Application.Inventory.StockAdjustments.Dtos;
using CSM.Infrastructure.Persistence;

namespace CSM.Infrastructure.Services;

public sealed class StockAdjustmentService
    : IStockAdjustmentService
{
    private readonly ApplicationDbContext _dbContext;

    public StockAdjustmentService(
        ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<StockAdjustmentResponse>> GetAllAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(currentUserId, cancellationToken);

        EnsureCanManage(actor);

        var query = _dbContext.StockMovements
            .AsNoTracking()
            .Include(x => x.StockItem)
                .ThenInclude(x => x.Material)
            .Include(x => x.StockItem)
                .ThenInclude(x => x.ConstructionSite)
            .Where(x => x.ReferenceType == "StockAdjustment");

        if (!IsSuperAdmin(actor))
        {
            query = query.Where(x => x.CompanyId == actor.CompanyId);
        }

        var records = await query
            .OrderByDescending(x => x.MovementDateUtc)
            .ToListAsync(cancellationToken);

        return records
            .Select(x => new StockAdjustmentResponse
            {
                MovementId = x.Id,

                StockItemId = x.StockItemId,

                MaterialId = x.StockItem.MaterialId,
                MaterialCode = x.StockItem.Material.MaterialCode,
                MaterialName = x.StockItem.Material.Name,
                UnitOfMeasure = x.StockItem.Material.UnitOfMeasure,

                ConstructionSiteId = x.StockItem.ConstructionSiteId,
                ConstructionSiteName = x.StockItem.ConstructionSite.Name,

                Increase = x.MovementType == StockMovementType.AdjustmentIncrease,

                Quantity = x.Quantity,
                UnitCost = x.UnitCost ?? 0m,

                QuantityBefore = x.StockBalanceBefore ?? 0m,
                QuantityAfter = x.StockBalanceAfter ?? 0m,

                AdjustmentDateUtc = x.MovementDateUtc,
                AdjustmentNumber = x.ReferenceNumber ?? string.Empty,

                Reason = x.Remarks ?? string.Empty,
                ApprovedBy = null,
                Remarks = x.Remarks
            })
            .ToList();
    }

    public async Task<StockAdjustmentResponse> CreateAsync(
    Guid currentUserId,
    CreateStockAdjustmentRequest request,
    CancellationToken cancellationToken = default)
{
    var actor = await GetActorAsync(
        currentUserId,
        cancellationToken);

    EnsureCanManage(actor);

    ValidateRequest(request);

    var normalizedAdjustmentNumber =
        request.AdjustmentNumber
            .Trim()
            .ToUpperInvariant();

    var strategy =
        _dbContext.Database.CreateExecutionStrategy();

    Guid movementId = Guid.Empty;

decimal quantityBefore = 0m;
decimal quantityAfter = 0m;

    await strategy.ExecuteAsync(
        async () =>
        {
            await using var transaction =
                await _dbContext.Database.BeginTransactionAsync(
                    cancellationToken);

            try
            {
                var stockItem =
                    await _dbContext.StockItems
                        .Include(x => x.Material)
                        .Include(x => x.ConstructionSite)
                        .SingleOrDefaultAsync(
                            x =>
                                x.ConstructionSiteId ==
                                    request.ConstructionSiteId &&
                                x.MaterialId ==
                                    request.MaterialId,
                            cancellationToken)
                    ?? throw new StockAdjustmentManagementException(
                        "Stock item was not found.");

                EnsureCompanyAccess(
                    actor,
                    stockItem.CompanyId);

                var duplicate =
                    await _dbContext.StockMovements
                        .AnyAsync(
                            x =>
                                x.CompanyId ==
                                    stockItem.CompanyId &&
                                x.ReferenceNumber != null &&
                                x.ReferenceNumber.ToUpper() ==
                                    normalizedAdjustmentNumber,
                            cancellationToken);

                if (duplicate)
                {
                    throw new StockAdjustmentManagementException(
                        "An adjustment with this number already exists.");
                }

                                quantityBefore =
    stockItem.QuantityOnHand;

if (request.Increase)
{
    stockItem.QuantityOnHand +=
        request.Quantity;
}
else
{
    var available =
        stockItem.QuantityOnHand -
        stockItem.QuantityReserved;

    if (available < request.Quantity)
    {
        throw new StockAdjustmentManagementException(
            "Insufficient available stock.");
    }

    stockItem.QuantityOnHand -=
        request.Quantity;
}

quantityAfter =
    stockItem.QuantityOnHand;

                var movement = new StockMovement
                {
                    CompanyId = stockItem.CompanyId,
                    StockItemId = stockItem.Id,

                    MovementType = request.Increase
                        ? StockMovementType.AdjustmentIncrease
                        : StockMovementType.AdjustmentDecrease,

                    MovementDateUtc = request.AdjustmentDateUtc,

                    Quantity = request.Quantity,

                    // ADD THESE TWO LINES
                    StockBalanceBefore = quantityBefore,
                    StockBalanceAfter = quantityAfter,

                    UnitCost = request.UnitCost ?? stockItem.AverageUnitCost,

                    ReferenceType = "StockAdjustment",
                    ReferenceNumber = normalizedAdjustmentNumber,

                    Remarks = $"Reason: {request.Reason}" +
                              (string.IsNullOrWhiteSpace(request.Remarks)
                                  ? string.Empty
                                  : Environment.NewLine + request.Remarks)
                };

                _dbContext.StockMovements.Add(
                    movement);

                await _dbContext.SaveChangesAsync(
                    cancellationToken);

                movementId = movement.Id;

                await transaction.CommitAsync(
                    cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(
                    cancellationToken);

                throw;
            }
        });

    return await GetResponseAsync(
        movementId,
        quantityBefore,
        quantityAfter,
        request.Reason,
        request.ApprovedBy,
        request.Remarks,
        cancellationToken);
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
            ?? throw new StockAdjustmentManagementException(
                "Authenticated user was not found.");
    }

    private static void EnsureCanManage(User actor)
    {
        if (HasRole(actor, AppRoles.Administrator) ||
            HasRole(actor, AppRoles.Storekeeper) ||
            HasRole(actor, AppRoles.ProcurementOfficer))
        {
            return;
        }

        throw new StockAdjustmentManagementException(
            "You are not authorized to manage stock adjustments.");
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
            throw new StockAdjustmentManagementException(
                "You are not authorized to access this company.");
        }
    }

    private static Guid GetActorCompanyId(
        User actor)
    {
        if (!actor.CompanyId.HasValue)
        {
            throw new StockAdjustmentManagementException(
                "Current user is not assigned to a company.");
        }

        return actor.CompanyId.Value;
    }

    private static bool IsSuperAdmin(
        User user)
    {
        return HasRole(
            user,
            AppRoles.Administrator);
    }

    private static bool HasRole(
        User user,
        string roleName)
    {
        return user.UserRoles.Any(
            x =>
                !x.IsDeleted &&
                !x.Role.IsDeleted &&
                x.Role.Name.Equals(
                    roleName,
                    StringComparison.OrdinalIgnoreCase));
    }


    private static void ValidateRequest(
        CreateStockAdjustmentRequest request)
    {
        if (request.ConstructionSiteId == Guid.Empty)
        {
            throw new StockAdjustmentManagementException(
                "Construction site is required.");
        }

        if (request.MaterialId == Guid.Empty)
        {
            throw new StockAdjustmentManagementException(
                "Material is required.");
        }

        if (request.Quantity <= 0)
        {
            throw new StockAdjustmentManagementException(
                "Quantity must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(
            request.AdjustmentNumber))
        {
            throw new StockAdjustmentManagementException(
                "Adjustment number is required.");
        }

        if (string.IsNullOrWhiteSpace(
            request.Reason))
        {
            throw new StockAdjustmentManagementException(
                "Reason is required.");
        }
    }

    private async Task<StockAdjustmentResponse> GetResponseAsync(
        Guid movementId,
        decimal quantityBefore,
        decimal quantityAfter,
        string reason,
        string? approvedBy,
        string? remarks,
        CancellationToken cancellationToken)
{
    var movement =
        await _dbContext.StockMovements
            .AsNoTracking()
            .Include(x => x.StockItem)
                .ThenInclude(x => x.Material)
            .Include(x => x.StockItem)
                .ThenInclude(x => x.ConstructionSite)
            .SingleOrDefaultAsync(
                x => x.Id == movementId,
                cancellationToken)
        ?? throw new StockAdjustmentManagementException(
            "Stock adjustment was not found.");

    return new StockAdjustmentResponse
    {
        MovementId = movement.Id,

        StockItemId = movement.StockItemId,

        MaterialId = movement.StockItem.MaterialId,

        MaterialCode = movement.StockItem.Material.MaterialCode,

        MaterialName = movement.StockItem.Material.Name,

        UnitOfMeasure = movement.StockItem.Material.UnitOfMeasure,

        ConstructionSiteId =
            movement.StockItem.ConstructionSiteId,

        ConstructionSiteName =
            movement.StockItem.ConstructionSite.Name,

        Increase =
            movement.MovementType ==
            StockMovementType.AdjustmentIncrease,

        Quantity = movement.Quantity,

        UnitCost =
            movement.UnitCost ??
            movement.StockItem.AverageUnitCost,

        QuantityBefore = quantityBefore,

QuantityAfter = quantityAfter,

        AdjustmentDateUtc =
            movement.MovementDateUtc,

        AdjustmentNumber =
            movement.ReferenceNumber ??
            string.Empty,

        Reason = reason,

        ApprovedBy = approvedBy,

        Remarks = remarks
    };
}

}


