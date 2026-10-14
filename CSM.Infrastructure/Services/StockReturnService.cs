using CSM.Application.Common.Exceptions;
using CSM.Application.Inventory.StockReturns;
using CSM.Application.Inventory.StockReturns.Dtos;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Procurement;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public sealed class StockReturnService : IStockReturnService
{
    private readonly ApplicationDbContext _dbContext;

    public StockReturnService(
        ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<StockReturnResponse>> GetAllAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(currentUserId, cancellationToken);

        EnsureCanManage(actor);

        var query = _dbContext.StockMovements
            .AsNoTracking()
            .Where(x => x.MovementType == StockMovementType.Return)
            .Include(x => x.StockItem)
                .ThenInclude(x => x.ConstructionSite)
            .Include(x => x.StockItem)
                .ThenInclude(x => x.Material)
            .AsQueryable();

        if (!IsSuperAdmin(actor))
        {
            query = query.Where(x => x.CompanyId == actor.CompanyId);
        }

        var records = await query
            .OrderByDescending(x => x.MovementDateUtc)
            .ToListAsync(cancellationToken);

        return records.Select(Map).ToList();
    }

    public async Task<StockReturnResponse> GetByIdAsync(
        Guid currentUserId,
        Guid stockReturnId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(currentUserId, cancellationToken);

        EnsureCanManage(actor);

        var query = _dbContext.StockMovements
            .AsNoTracking()
            .Where(x =>
                x.Id == stockReturnId &&
                x.MovementType == StockMovementType.Return)
            .Include(x => x.StockItem)
                .ThenInclude(x => x.ConstructionSite)
            .Include(x => x.StockItem)
                .ThenInclude(x => x.Material)
            .AsQueryable();

        if (!IsSuperAdmin(actor))
        {
            query = query.Where(x => x.CompanyId == actor.CompanyId);
        }

        var movement = await query.SingleOrDefaultAsync(cancellationToken)
            ?? throw new StockReturnManagementException("Stock return was not found.");

        return Map(movement);
    }

    public async Task<StockReturnResponse> CreateAsync(
        Guid currentUserId,
        CreateStockReturnRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanManage(actor);

        ValidateRequest(request);

        var normalizedReferenceNumber =
            request.ReferenceNumber
                .Trim()
                .ToUpperInvariant();

        var strategy =
            _dbContext.Database.CreateExecutionStrategy();

        Guid returnMovementId = Guid.Empty;
        Guid stockItemId = Guid.Empty;
        Guid constructionSiteId = Guid.Empty;
        Guid materialId = Guid.Empty;

        string siteName = string.Empty;
        string materialCode = string.Empty;
        string materialName = string.Empty;
        string unitOfMeasure = string.Empty;

        decimal originalIssuedQuantity = 0m;
        decimal previouslyReturnedQuantity = 0m;
        decimal remainingReturnableQuantity = 0m;
        decimal quantityBefore = 0m;
        decimal quantityAfter = 0m;
        decimal unitCost = 0m;

        await strategy.ExecuteAsync(
            async () =>
            {
                await using var transaction =
                    await _dbContext.Database
                        .BeginTransactionAsync(
                            cancellationToken);

                try
                {
                    var originalIssue =
                        await _dbContext.StockMovements
                            .Include(x => x.StockItem)
                                .ThenInclude(x =>
                                    x.ConstructionSite)
                            .Include(x => x.StockItem)
                                .ThenInclude(x =>
                                    x.Material)
                            .SingleOrDefaultAsync(
                                x =>
                                    x.Id ==
                                        request.OriginalIssueMovementId,
                                cancellationToken)
                        ?? throw new StockReturnManagementException(
                            "Original stock issue movement was not found.");

                    if (originalIssue.MovementType !=
                        StockMovementType.Issue)
                    {
                        throw new StockReturnManagementException(
                            "The selected stock movement is not an issue.");
                    }

                    EnsureCompanyAccess(
                        actor,
                        originalIssue.CompanyId);

                    var duplicateReference =
                        await _dbContext.StockMovements
                            .AnyAsync(
                                x =>
                                    x.CompanyId ==
                                        originalIssue.CompanyId &&
                                    x.MovementType ==
                                        StockMovementType.Return &&
                                    x.ReferenceNumber ==
                                        normalizedReferenceNumber,
                                cancellationToken);

                    if (duplicateReference)
                    {
                        throw new StockReturnManagementException(
                            "A stock return with this reference number already exists in the company.");
                    }

                    var stockItem =
                        originalIssue.StockItem;

                    if (stockItem.CompanyId !=
                        originalIssue.CompanyId)
                    {
                        throw new StockReturnManagementException(
                            "The stock item does not belong to the original issue company.");
                    }

                    previouslyReturnedQuantity =
                        await _dbContext.StockMovements
                            .Where(
                                x =>
                                    x.CompanyId ==
                                        originalIssue.CompanyId &&
                                    x.MovementType ==
                                        StockMovementType.Return &&
                                    x.ReferenceId ==
                                        originalIssue.Id)
                            .SumAsync(
                                x => (decimal?)x.Quantity,
                                cancellationToken)
                        ?? 0m;

                    originalIssuedQuantity =
                        originalIssue.Quantity;

                    var availableToReturn =
                        originalIssuedQuantity -
                        previouslyReturnedQuantity;

                    if (availableToReturn < 0m)
                    {
                        availableToReturn = 0m;
                    }

                    if (request.Quantity >
                        availableToReturn)
                    {
                        throw new StockReturnManagementException(
                            "Return quantity exceeds the remaining returnable quantity for the original issue.");
                    }

                    stockItemId =
                        stockItem.Id;

                    constructionSiteId =
                        stockItem.ConstructionSiteId;

                    materialId =
                        stockItem.MaterialId;

                    siteName =
                        stockItem.ConstructionSite.Name;

                    materialCode =
                        stockItem.Material.MaterialCode;

                    materialName =
                        stockItem.Material.Name;

                    unitOfMeasure =
                        stockItem.Material.UnitOfMeasure;

                    quantityBefore =
                        stockItem.QuantityOnHand;

                    unitCost =
                        originalIssue.UnitCost ??
                        stockItem.AverageUnitCost;

                    stockItem.QuantityOnHand +=
                        request.Quantity;

                    quantityAfter =
                        stockItem.QuantityOnHand;

                    remainingReturnableQuantity =
                        availableToReturn -
                        request.Quantity;

                    var movement =
                        new StockMovement
                        {
                            CompanyId =
                                originalIssue.CompanyId,

                            StockItemId =
                                stockItem.Id,

                            MovementType =
                                StockMovementType.Return,

                            MovementDateUtc =
                                request.ReturnDateUtc,

                            Quantity =
                                request.Quantity,

                            StockBalanceBefore =
                                quantityBefore,

                            StockBalanceAfter =
                                quantityAfter,

                            UnitCost =
                                unitCost,

                            ReferenceType =
                                "StockReturn",

                            ReferenceId =
                                originalIssue.Id,

                            ReferenceNumber =
                                normalizedReferenceNumber,

                            Remarks =
                                BuildRemarks(request)
                        };

                    _dbContext.StockMovements.Add(
                        movement);

                    await _dbContext.SaveChangesAsync(
                        cancellationToken);

                    returnMovementId =
                        movement.Id;

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

        return new StockReturnResponse
        {
            Id =
                returnMovementId,

            OriginalIssueMovementId =
                request.OriginalIssueMovementId,

            StockItemId =
                stockItemId,

            ConstructionSiteId =
                constructionSiteId,

            SiteName =
                siteName,

            MaterialId =
                materialId,

            MaterialCode =
                materialCode,

            MaterialName =
                materialName,

            UnitOfMeasure =
                unitOfMeasure,

            OriginalIssuedQuantity =
                originalIssuedQuantity,

            PreviouslyReturnedQuantity =
                previouslyReturnedQuantity,

            ReturnedQuantity =
                request.Quantity,

            RemainingReturnableQuantity =
                remainingReturnableQuantity,

            UnitCost =
                unitCost,

            QuantityOnHandBefore =
                quantityBefore,

            QuantityOnHandAfter =
                quantityAfter,

            ReturnDateUtc =
                request.ReturnDateUtc,

            ReferenceNumber =
                normalizedReferenceNumber,

            ReturnedBy =
                Clean(request.ReturnedBy),

            Reason =
                Clean(request.Reason),

            Remarks =
                Clean(request.Remarks)
        };
    }

    private StockReturnResponse Map(StockMovement movement)
    {
        var remarks = movement.Remarks ?? string.Empty;

        string? returnedBy = null;
        string? reason = null;
        string? note = null;

        foreach (var part in remarks.Split(" | ", StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.StartsWith("Returned By: "))
                returnedBy = part["Returned By: ".Length..];
            else if (part.StartsWith("Reason: "))
                reason = part["Reason: ".Length..];
            else
                note = part;
        }

        var originalIssue = _dbContext.StockMovements
            .AsNoTracking()
            .FirstOrDefault(x => x.Id == movement.ReferenceId);

        var originalIssued = originalIssue?.Quantity ?? 0m;

        var previouslyReturned = _dbContext.StockMovements
            .AsNoTracking()
            .Where(x =>
                x.MovementType == StockMovementType.Return &&
                x.ReferenceId == movement.ReferenceId &&
                x.Id != movement.Id)
            .Sum(x => (decimal?)x.Quantity) ?? 0m;

        return new StockReturnResponse
        {
            Id = movement.Id,
            OriginalIssueMovementId = movement.ReferenceId ?? Guid.Empty,
            StockItemId = movement.StockItemId,
            ConstructionSiteId = movement.StockItem.ConstructionSiteId,
            SiteName = movement.StockItem.ConstructionSite.Name,
            MaterialId = movement.StockItem.MaterialId,
            MaterialCode = movement.StockItem.Material.MaterialCode,
            MaterialName = movement.StockItem.Material.Name,
            UnitOfMeasure = movement.StockItem.Material.UnitOfMeasure,

            OriginalIssuedQuantity = originalIssued,
            PreviouslyReturnedQuantity = previouslyReturned,
            ReturnedQuantity = movement.Quantity,
            RemainingReturnableQuantity =
                originalIssued - previouslyReturned - movement.Quantity,

            UnitCost = movement.UnitCost ?? 0m,
            QuantityOnHandBefore = movement.StockBalanceBefore ?? 0m,
            QuantityOnHandAfter = movement.StockBalanceAfter ?? 0m,
            ReturnDateUtc = movement.MovementDateUtc,
            ReferenceNumber = movement.ReferenceNumber ?? string.Empty,
            ReturnedBy = returnedBy,
            Reason = reason,
            Remarks = note
        };
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
            ?? throw new StockReturnManagementException(
                "Current user was not found.");
    }

    private static void ValidateRequest(
        CreateStockReturnRequest request)
    {
        if (request.OriginalIssueMovementId ==
            Guid.Empty)
        {
            throw new StockReturnManagementException(
                "Original issue movement is required.");
        }

        if (request.Quantity <= 0m)
        {
            throw new StockReturnManagementException(
                "Return quantity must be greater than zero.");
        }

        if (request.ReturnDateUtc ==
            default)
        {
            throw new StockReturnManagementException(
                "Return date is required.");
        }

        if (string.IsNullOrWhiteSpace(
                request.ReferenceNumber))
        {
            throw new StockReturnManagementException(
                "Reference number is required.");
        }
    }

    private static void EnsureCanManage(User actor)
    {
        if (HasRole(actor, AppRoles.Administrator) ||
            HasRole(actor, AppRoles.Storekeeper) ||
            HasRole(actor, AppRoles.ProcurementOfficer))
        {
            return;
        }

        throw new StockReturnManagementException(
            "You are not authorized to manage stock returns.");
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
            throw new StockReturnManagementException(
                "You are not authorized to access this company stock.");
        }
    }

    private static bool IsSuperAdmin(
        User actor)
    {
        return HasRole(
            actor,
            AppRoles.Administrator);
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

    private static string? BuildRemarks(
        CreateStockReturnRequest request)
    {
        var parts = new List<string>();

        var returnedBy =
            Clean(request.ReturnedBy);

        var reason =
            Clean(request.Reason);

        var remarks =
            Clean(request.Remarks);

        if (returnedBy is not null)
        {
            parts.Add(
                $"Returned By: {returnedBy}");
        }

        if (reason is not null)
        {
            parts.Add(
                $"Reason: {reason}");
        }

        if (remarks is not null)
        {
            parts.Add(remarks);
        }

        return parts.Count == 0
            ? null
            : string.Join(
                " | ",
                parts);
    }
}

