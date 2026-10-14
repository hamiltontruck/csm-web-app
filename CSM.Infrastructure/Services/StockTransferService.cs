using CSM.Application.Common.Exceptions;
using CSM.Application.Inventory.StockTransfers;
using CSM.Application.Inventory.StockTransfers.Dtos;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Procurement;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using CSM.Domain.Enums;

namespace CSM.Infrastructure.Services;

public sealed partial class StockTransferService
    : IStockTransferService
{
    private readonly ApplicationDbContext _dbContext;

    public StockTransferService(
        ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<StockTransferResponse>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var transfers = await _dbContext.StockMovements
            .AsNoTracking()
            .Include(x => x.StockItem)
                .ThenInclude(x => x.Material)
            .Include(x => x.StockItem)
                .ThenInclude(x => x.ConstructionSite)
            .Where(x =>
                x.ReferenceType == "StockTransfer" &&
                x.MovementType == StockMovementType.TransferOut)
            .OrderByDescending(x => x.MovementDateUtc)
            .ToListAsync(cancellationToken);

        var results = new List<StockTransferResponse>();

        foreach (var movement in transfers)
        {
            var destination = await _dbContext.StockMovements
                .AsNoTracking()
                .Include(x => x.StockItem)
                    .ThenInclude(x => x.ConstructionSite)
                .FirstOrDefaultAsync(x =>
                    x.ReferenceType == "StockTransfer" &&
                    x.ReferenceNumber == movement.ReferenceNumber &&
                    x.MovementType == StockMovementType.TransferIn,
                    cancellationToken);

            results.Add(new StockTransferResponse
            {
                TransferOutMovementId = movement.Id,
                TransferInMovementId = destination?.Id ?? Guid.Empty,

                MaterialId = movement.StockItem.MaterialId,
                MaterialCode = movement.StockItem.Material.MaterialCode,
                MaterialName = movement.StockItem.Material.Name,
                UnitOfMeasure = movement.StockItem.Material.UnitOfMeasure,

                SourceConstructionSiteId = movement.StockItem.ConstructionSiteId,
                SourceSiteName = movement.StockItem.ConstructionSite.Name,

                DestinationConstructionSiteId = destination?.StockItem.ConstructionSiteId ?? Guid.Empty,
                DestinationSiteName = destination?.StockItem.ConstructionSite.Name ?? "",

                FromSiteId = movement.StockItem.ConstructionSiteId,
                ToSiteId = destination?.StockItem.ConstructionSiteId ?? Guid.Empty,

                FromSiteName = movement.StockItem.ConstructionSite.Name,
                ToSiteName = destination?.StockItem.ConstructionSite.Name ?? "",

                Quantity = movement.Quantity,
                UnitCost = movement.UnitCost ?? 0m,

                SourceQuantityBefore = 0,
                SourceQuantityAfter = 0,
                DestinationQuantityBefore = 0,
                DestinationQuantityAfter = 0,

                TransferDateUtc = movement.MovementDateUtc,
                TransferNumber = movement.ReferenceNumber ?? "",

                Remarks = movement.Remarks
            });
        }

        return results;
    }

    public async Task<StockTransferResponse> CreateAsync(
        Guid companyId,
        CreateStockTransferRequest request,
        CancellationToken cancellationToken = default)
    {

        // Company scope is provided by the authenticated API caller.

        ValidateRequest(request);

        var normalizedTransferNumber =
            request.TransferNumber
                .Trim()
                .ToUpperInvariant();

        var strategy =
            _dbContext.Database
                .CreateExecutionStrategy();

        Guid transferOutMovementId = Guid.Empty;
        Guid transferInMovementId = Guid.Empty;

        decimal sourceQuantityBefore = 0m;
        decimal sourceQuantityAfter = 0m;

        decimal destinationQuantityBefore = 0m;
        decimal destinationQuantityAfter = 0m;

        decimal unitCost = 0m;

        string sourceSiteName = string.Empty;
        string destinationSiteName = string.Empty;

        string materialCode = string.Empty;
        string materialName = string.Empty;
        string unitOfMeasure = string.Empty;

        Guid materialId = Guid.Empty;

        await strategy.ExecuteAsync(
            async () =>
            {
                await using var transaction =
                    await _dbContext.Database
                        .BeginTransactionAsync(
                            cancellationToken);

                try
                {
                    var sourceStock =
                        await _dbContext.StockItems
                            .Include(x => x.ConstructionSite)
                            .Include(x => x.Material)
                            .SingleOrDefaultAsync(
                                x =>
                                    x.ConstructionSiteId ==
                                        request.SourceConstructionSiteId &&
                                    x.MaterialId ==
                                        request.MaterialId,
                                cancellationToken)
                        ?? throw new StockTransferManagementException(
                            "Source stock item was not found.");

                    if (sourceStock.CompanyId != companyId)
                    {
                        throw new StockTransferManagementException(
                            "You are not authorized to access this company.");
                    }

                    var duplicateTransfer =
                        await _dbContext.StockMovements
                            .AnyAsync(
                                x =>
                                    x.CompanyId ==
                                        sourceStock.CompanyId &&
                                    x.ReferenceType ==
                                        "StockTransfer" &&
                                    x.ReferenceNumber ==
                                        normalizedTransferNumber,
                                cancellationToken);

                    if (duplicateTransfer)
                    {
                        throw new StockTransferManagementException(
                            "A stock transfer with this transfer number already exists.");
                    }
                    var available =
                        sourceStock.QuantityOnHand -
                        sourceStock.QuantityReserved;

                    if (request.Quantity > available)
                    {
                        throw new StockTransferManagementException(
                            "Insufficient available stock.");
                    }
                    var destinationStock =
                        await _dbContext.StockItems
                            .Include(x => x.ConstructionSite)
                            .Include(x => x.Material)
                            .SingleOrDefaultAsync(
                                x =>
                                    x.ConstructionSiteId ==
                                        request.DestinationConstructionSiteId &&
                                    x.MaterialId ==
                                        request.MaterialId,
                                cancellationToken);

                                if (destinationStock is null)
                                {
                                    destinationStock =
                                        new StockItem
                                        {
                                            CompanyId =
                                                sourceStock.CompanyId,

                                            ConstructionSiteId =
                                                request.DestinationConstructionSiteId,

                                            MaterialId =
                                                sourceStock.MaterialId,

                                            QuantityOnHand = 0m,

                                            QuantityReserved = 0m,

                                            AverageUnitCost =
                                                sourceStock.AverageUnitCost,

                                            ReorderLevel =
                                                sourceStock.ReorderLevel,

                                            MaximumStockLevel =
                                                sourceStock.MaximumStockLevel,

                                            StorageLocation =
                                                sourceStock.StorageLocation
                                        };

                                    _dbContext.StockItems.Add(
                                        destinationStock);

                                    await _dbContext.SaveChangesAsync(
                                        cancellationToken);

                                    destinationStock =
                                        await _dbContext.StockItems
                                            .Include(x => x.ConstructionSite)
                                            .Include(x => x.Material)
                                            .SingleAsync(
                                                x => x.Id == destinationStock.Id,
                                                cancellationToken);
                                }
                                sourceQuantityBefore =
                                    sourceStock.QuantityOnHand;

                                destinationQuantityBefore =
                                    destinationStock.QuantityOnHand;
                                    sourceStock.QuantityOnHand -=
                                        request.Quantity;

                                    destinationStock.QuantityOnHand +=
                                        request.Quantity;

                                    sourceQuantityAfter =
                                        sourceStock.QuantityOnHand;

                                    destinationQuantityAfter =
                                        destinationStock.QuantityOnHand;

                                    unitCost =
                                        sourceStock.AverageUnitCost;

                                    sourceSiteName =
                                        sourceStock.ConstructionSite.Name;

                                    destinationSiteName =
                                        destinationStock.ConstructionSite.Name;

                                    materialId =
                                        sourceStock.MaterialId;

                                    materialCode =
                                        sourceStock.Material.MaterialCode;

                                    materialName =
                                        sourceStock.Material.Name;

                                    unitOfMeasure =
                                        sourceStock.Material.UnitOfMeasure;
                                    var transferOut = new StockMovement
                                    {
                                        CompanyId = sourceStock.CompanyId,
                                        StockItemId = sourceStock.Id,
                                        MovementType = StockMovementType.TransferOut,
                                        MovementDateUtc = request.TransferDateUtc,
                                        Quantity = request.Quantity,

                                        StockBalanceBefore = sourceQuantityBefore,
                                        StockBalanceAfter = sourceQuantityAfter,

                                        UnitCost = unitCost,
                                        ReferenceType = "StockTransfer",
                                        ReferenceNumber = normalizedTransferNumber,
                                        Remarks = BuildRemarks(request)
                                    };

                                    _dbContext.StockMovements.Add(
                                        transferOut);

                                    await _dbContext.SaveChangesAsync(
                                        cancellationToken);

                                    var transferIn = new StockMovement
                                    {
                                        CompanyId = destinationStock.CompanyId,
                                        StockItemId = destinationStock.Id,

                                        MovementType = StockMovementType.TransferIn,
                                        MovementDateUtc = request.TransferDateUtc,

                                        Quantity = request.Quantity,

                                        StockBalanceBefore = destinationQuantityBefore,
                                        StockBalanceAfter = destinationQuantityAfter,

                                        UnitCost = unitCost,

                                        ReferenceType = "StockTransfer",
                                        ReferenceId = transferOut.Id,
                                        ReferenceNumber = normalizedTransferNumber,

                                        Remarks = BuildRemarks(request)
                                    };

                                    _dbContext.StockMovements.Add(
                                        transferIn);

                                    await _dbContext.SaveChangesAsync(
                                        cancellationToken);

                                    transferOutMovementId =
                                        transferOut.Id;

                                    transferInMovementId =
                                        transferIn.Id;

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

        return new StockTransferResponse
        {
            Id = transferOutMovementId,

            TransferOutMovementId = transferOutMovementId,
            TransferInMovementId = transferInMovementId,

            SourceConstructionSiteId = request.SourceConstructionSiteId,
            DestinationConstructionSiteId = request.DestinationConstructionSiteId,

            FromSiteId = request.SourceConstructionSiteId,
            ToSiteId = request.DestinationConstructionSiteId,

            SourceSiteName = sourceSiteName,
            DestinationSiteName = destinationSiteName,

            FromSiteName = sourceSiteName,
            ToSiteName = destinationSiteName,

            MaterialId = materialId,
            MaterialCode = materialCode,
            MaterialName = materialName,
            UnitOfMeasure = unitOfMeasure,

            Quantity = request.Quantity,
            UnitCost = unitCost,

            SourceQuantityBefore = sourceQuantityBefore,
            SourceQuantityAfter = sourceQuantityAfter,

            DestinationQuantityBefore = destinationQuantityBefore,
            DestinationQuantityAfter = destinationQuantityAfter,



            TransferDateUtc = request.TransferDateUtc,

            TransferNumber = normalizedTransferNumber,
            ReferenceNumber = normalizedTransferNumber,

            Status = "Completed",
            Remarks = request.Remarks
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
            ?? throw new StockTransferManagementException(
                "Current user was not found.");
    }

    private static void ValidateRequest(
        CreateStockTransferRequest request)
    {
        if (request.SourceConstructionSiteId == Guid.Empty)
        {
            throw new StockTransferManagementException(
                "Source construction site is required.");
        }

        if (request.DestinationConstructionSiteId == Guid.Empty)
        {
            throw new StockTransferManagementException(
                "Destination construction site is required.");
        }

        if (request.SourceConstructionSiteId ==
            request.DestinationConstructionSiteId)
        {
            throw new StockTransferManagementException(
                "Source and destination construction sites must be different.");
        }

        if (request.MaterialId == Guid.Empty)
        {
            throw new StockTransferManagementException(
                "Material is required.");
        }

        if (request.Quantity <= 0)
        {
            throw new StockTransferManagementException(
                "Transfer quantity must be greater than zero.");
        }

        if (request.TransferDateUtc == default)
        {
            throw new StockTransferManagementException(
                "Transfer date is required.");
        }

        if (string.IsNullOrWhiteSpace(request.TransferNumber))
        {
            throw new StockTransferManagementException(
                "Transfer number is required.");
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

        throw new StockTransferManagementException(
            "You are not authorized to manage stock transfers.");
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
            throw new StockTransferManagementException(
                "You are not authorized to access this company stock.");
        }
    }

    private static bool IsSuperAdmin(
        User actor)
    {
        return HasRole(actor, AppRoles.Administrator);
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
        CreateStockTransferRequest request)
    {
        var parts = new List<string>();

        var requestedBy =
            Clean(request.RequestedBy);

        var approvedBy =
            Clean(request.ApprovedBy);

        var remarks =
            Clean(request.Remarks);

        if (requestedBy is not null)
        {
            parts.Add(
                $"Requested By: {requestedBy}");
        }

        if (approvedBy is not null)
        {
            parts.Add(
                $"Approved By: {approvedBy}");
        }

        if (remarks is not null)
        {
            parts.Add(remarks);
        }

        return parts.Count == 0
            ? null
            : string.Join(" | ", parts);
    }
}