using CSM.Application.Common.Exceptions;
using CSM.Application.Procurement.GoodsReceipts;
using CSM.Application.Procurement.GoodsReceipts.Dtos;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Procurement;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public sealed class GoodsReceiptService : IGoodsReceiptService
{
    private readonly ApplicationDbContext _dbContext;

    public GoodsReceiptService(
        ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<GoodsReceiptResponse>> GetAllAsync(
        Guid currentUserId,
        Guid? constructionSiteId = null,
        Guid? purchaseOrderId = null,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanManage(actor);

        var query = BuildQuery();

        if (!IsSuperAdmin(actor))
        {
            var companyId = GetActorCompanyId(actor);

            query = query.Where(
                x => x.CompanyId == companyId);
        }

        if (constructionSiteId.HasValue)
        {
            query = query.Where(
                x => x.ConstructionSiteId ==
                     constructionSiteId.Value);
        }

        if (purchaseOrderId.HasValue)
        {
            query = query.Where(
                x => x.PurchaseOrderId ==
                     purchaseOrderId.Value);
        }

        var records = await query
            .OrderByDescending(x => x.ReceivedAtUtc)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var goodsReceiptIds = records
            .Select(x => x.Id)
            .ToList();

        var stockMovements = await _dbContext.StockMovements
            .AsNoTracking()
            .Include(x => x.StockItem)
            .Where(
                x =>
                    x.ReferenceType == "GoodsReceipt" &&
                    x.ReferenceId.HasValue &&
                    goodsReceiptIds.Contains(x.ReferenceId.Value))
            .ToListAsync(cancellationToken);

        return records
            .Select(
                record =>
                    Map(
                        record,
                        stockMovements
                            .Where(
                                movement =>
                                    movement.ReferenceId == record.Id)
                            .ToList()))
            .ToList();
    }

    public async Task<GoodsReceiptResponse> GetByIdAsync(
        Guid currentUserId,
        Guid goodsReceiptId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanManage(actor);

        var record = await GetGoodsReceiptAsync(
            goodsReceiptId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            record.CompanyId);

        var stockMovements = await _dbContext.StockMovements
            .AsNoTracking()
            .Include(x => x.StockItem)
            .Where(
                x =>
                    x.ReferenceType == "GoodsReceipt" &&
                    x.ReferenceId == goodsReceiptId)
            .ToListAsync(cancellationToken);

        return Map(
            record,
            stockMovements);
    }

    public async Task<GoodsReceiptResponse> CreateAsync(
        Guid currentUserId,
        CreateGoodsReceiptRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanManage(actor);

        ValidateRequest(request);

        var normalizedReceiptNumber =
            request.ReceiptNumber.Trim().ToUpperInvariant();

        var strategy =
            _dbContext.Database.CreateExecutionStrategy();

        Guid goodsReceiptId = Guid.Empty;

        await strategy.ExecuteAsync(
            async () =>
            {
                await using var transaction =
                    await _dbContext.Database.BeginTransactionAsync(
                        cancellationToken);

                try
                {
                    var purchaseOrder =
                        await _dbContext.PurchaseOrders
                            .Include(x => x.Lines)
                            .SingleOrDefaultAsync(
                                x => x.Id == request.PurchaseOrderId,
                                cancellationToken)
                        ?? throw new GoodsReceiptManagementException(
                            "Purchase order was not found.");

                    EnsureCompanyAccess(
                        actor,
                        purchaseOrder.CompanyId);

                    if (purchaseOrder.Status is not
                        PurchaseOrderStatus.SentToVendor and not
                        PurchaseOrderStatus.PartiallyDelivered)
                    {
                        throw new GoodsReceiptManagementException(
                            "Only SentToVendor or PartiallyDelivered purchase orders can receive goods.");
                    }

                    if (purchaseOrder.ConstructionSiteId !=
                        request.ConstructionSiteId)
                    {
                        throw new GoodsReceiptManagementException(
                            "Goods must be received at the purchase order construction site.");
                    }

                    var duplicateReceipt =
                        await _dbContext.GoodsReceipts
                            .AnyAsync(
                                x =>
                                    x.CompanyId ==
                                        purchaseOrder.CompanyId &&
                                    x.ReceiptNumber.ToUpper() ==
                                        normalizedReceiptNumber,
                                cancellationToken);

                    if (duplicateReceipt)
                    {
                        throw new GoodsReceiptManagementException(
                            "A goods receipt with this receipt number already exists in the company.");
                    }

                    var receipt = new GoodsReceipt
                    {
                        CompanyId = purchaseOrder.CompanyId,
                        PurchaseOrderId = purchaseOrder.Id,
                        ConstructionSiteId =
                            purchaseOrder.ConstructionSiteId,
                        ReceiptNumber =
                            normalizedReceiptNumber,
                        ReceivedAtUtc =
                            request.ReceivedAtUtc,
                        ReceivedBy =
                            currentUserId,
                        DeliveryNoteNumber =
                            Clean(request.DeliveryNoteNumber),
                        VehiclePlateNumber =
                            Clean(request.VehiclePlateNumber),
                        Remarks =
                            Clean(request.Remarks),
                        CreatedBy =
                            currentUserId
                    };

                    foreach (var input in request.Lines)
                    {
                        var poLine =
                            purchaseOrder.Lines.SingleOrDefault(
                                x =>
                                    x.Id ==
                                    input.PurchaseOrderLineId);

                        if (poLine is null)
                        {
                            throw new GoodsReceiptManagementException(
                                "A goods receipt line does not belong to the selected purchase order.");
                        }

                        var remainingQuantity =
                            poLine.OrderedQuantity -
                            poLine.ReceivedQuantity;

                        if (input.ReceivedQuantity >
                            remainingQuantity)
                        {
                            throw new GoodsReceiptManagementException(
                                "Received quantity cannot exceed the remaining purchase order quantity.");
                        }

                        receipt.Lines.Add(
                            new GoodsReceiptLine
                            {
                                CompanyId =
                                    purchaseOrder.CompanyId,
                                PurchaseOrderLineId =
                                    poLine.Id,
                                MaterialId =
                                    poLine.MaterialId,
                                ReceivedQuantity =
                                    input.ReceivedQuantity,
                                AcceptedQuantity =
                                    input.AcceptedQuantity,
                                RejectedQuantity =
                                    input.RejectedQuantity,
                                RejectionReason =
                                    Clean(input.RejectionReason),
                                Remarks =
                                    Clean(input.Remarks),
                                CreatedBy =
                                    currentUserId
                            });

                        poLine.ReceivedQuantity +=
                            input.ReceivedQuantity;

                        poLine.UpdatedBy =
                            currentUserId;

                        if (input.AcceptedQuantity > 0m)
                        {
                            await ApplyAcceptedStockAsync(
                                purchaseOrder,
                                poLine,
                                receipt,
                                input.AcceptedQuantity,
                                currentUserId,
                                cancellationToken);

                            if (purchaseOrder.MaterialRequestId.HasValue)
                            {
                                await ApplyMaterialRequestDeliveryAsync(
                                    purchaseOrder.MaterialRequestId.Value,
                                    poLine.MaterialId,
                                    input.AcceptedQuantity,
                                    currentUserId,
                                    cancellationToken);
                            }
                        }
                    }

                    _dbContext.GoodsReceipts.Add(receipt);

                    SynchronizePurchaseOrderStatus(
                        purchaseOrder,
                        currentUserId);

                    if (purchaseOrder.MaterialRequestId.HasValue)
                    {
                        await SynchronizeMaterialRequestStatusAsync(
                            purchaseOrder.MaterialRequestId.Value,
                            currentUserId,
                            cancellationToken);
                    }

                    await _dbContext.SaveChangesAsync(
                        cancellationToken);

                    await transaction.CommitAsync(
                        cancellationToken);

                    goodsReceiptId = receipt.Id;
                }
                catch
                {
                    await transaction.RollbackAsync(
                        cancellationToken);

                    throw;
                }
            });

        return await GetResponseAsync(
            goodsReceiptId,
            cancellationToken);
    }

    private async Task ApplyAcceptedStockAsync(
        PurchaseOrder purchaseOrder,
        PurchaseOrderLine poLine,
        GoodsReceipt receipt,
        decimal acceptedQuantity,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var stockItem =
            await _dbContext.StockItems
                .SingleOrDefaultAsync(
                    x =>
                        x.ConstructionSiteId ==
                            purchaseOrder.ConstructionSiteId &&
                        x.MaterialId ==
                            poLine.MaterialId,
                    cancellationToken);

        if (stockItem is null)
        {
            stockItem = new StockItem
            {
                CompanyId =
                    purchaseOrder.CompanyId,
                ConstructionSiteId =
                    purchaseOrder.ConstructionSiteId,
                MaterialId =
                    poLine.MaterialId,
                QuantityOnHand =
                    0m,
                QuantityReserved =
                    0m,
                ReorderLevel =
                    0m,
                MaximumStockLevel =
                    null,
                AverageUnitCost =
                    0m,
                CreatedBy =
                    currentUserId
            };

            _dbContext.StockItems.Add(
                stockItem);
        }

        var oldQuantity =
            stockItem.QuantityOnHand;

        var oldValue =
            oldQuantity *
            stockItem.AverageUnitCost;

        var receivedValue =
            acceptedQuantity *
            poLine.UnitPrice;

        var newQuantity =
            oldQuantity +
            acceptedQuantity;

        stockItem.QuantityOnHand =
            newQuantity;

        stockItem.AverageUnitCost =
            newQuantity > 0m
                ? (oldValue + receivedValue) /
                  newQuantity
                : 0m;

        stockItem.UpdatedBy =
            currentUserId;

        var movement = new StockMovement
        {
            CompanyId =
                purchaseOrder.CompanyId,
            StockItem =
                stockItem,
            MovementType =
                StockMovementType.GoodsReceipt,
            MovementDateUtc =
                receipt.ReceivedAtUtc,
            Quantity =
                acceptedQuantity,
            StockBalanceBefore =
                oldQuantity,
            StockBalanceAfter =
                newQuantity,
            UnitCost =
                poLine.UnitPrice,
            ReferenceType =
                "GoodsReceipt",
            ReferenceId =
                receipt.Id,
            ReferenceNumber =
                receipt.ReceiptNumber,
            Remarks =
                Clean(receipt.Remarks),
            CreatedBy =
                currentUserId
        };

        _dbContext.StockMovements.Add(
            movement);
    }

    private async Task ApplyMaterialRequestDeliveryAsync(
        Guid materialRequestId,
        Guid materialId,
        decimal acceptedQuantity,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var line =
            await _dbContext.MaterialRequestLines
                .SingleOrDefaultAsync(
                    x =>
                        x.MaterialRequestId ==
                            materialRequestId &&
                        x.MaterialId ==
                            materialId,
                    cancellationToken)
            ?? throw new GoodsReceiptManagementException(
                "The purchase order material was not found on the linked material request.");

        var remainingApproved =
            line.ApprovedQuantity -
            line.DeliveredQuantity;

        if (acceptedQuantity >
            remainingApproved)
        {
            throw new GoodsReceiptManagementException(
                "Accepted quantity cannot exceed the remaining approved material request quantity.");
        }

        line.DeliveredQuantity +=
            acceptedQuantity;

        line.UpdatedBy =
            currentUserId;
    }

    private static void SynchronizePurchaseOrderStatus(
        PurchaseOrder purchaseOrder,
        Guid currentUserId)
    {
        var totalOrdered =
            purchaseOrder.Lines.Sum(
                x => x.OrderedQuantity);

        var totalReceived =
            purchaseOrder.Lines.Sum(
                x => x.ReceivedQuantity);

        purchaseOrder.Status =
            totalReceived >= totalOrdered
                ? PurchaseOrderStatus.Delivered
                : PurchaseOrderStatus.PartiallyDelivered;

        purchaseOrder.UpdatedBy =
            currentUserId;
    }

    private async Task SynchronizeMaterialRequestStatusAsync(
        Guid materialRequestId,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var materialRequest =
            await _dbContext.MaterialRequests
                .Include(x => x.Lines)
                .SingleOrDefaultAsync(
                    x => x.Id == materialRequestId,
                    cancellationToken)
            ?? throw new GoodsReceiptManagementException(
                "Linked material request was not found.");

        var totalApproved =
            materialRequest.Lines.Sum(
                x => x.ApprovedQuantity);

        var totalDelivered =
            materialRequest.Lines.Sum(
                x => x.DeliveredQuantity);

        if (totalDelivered >= totalApproved &&
            totalApproved > 0m)
        {
            materialRequest.Status =
                MaterialRequestStatus.Delivered;
        }
        else if (totalDelivered > 0m)
        {
            materialRequest.Status =
                MaterialRequestStatus.PartiallyDelivered;
        }

        materialRequest.UpdatedBy =
            currentUserId;
    }

    private static void ValidateRequest(
        CreateGoodsReceiptRequest request)
    {
        if (request.PurchaseOrderId == Guid.Empty)
        {
            throw new GoodsReceiptManagementException(
                "Purchase order is required.");
        }

        if (request.ConstructionSiteId == Guid.Empty)
        {
            throw new GoodsReceiptManagementException(
                "Construction site is required.");
        }

        if (string.IsNullOrWhiteSpace(
                request.ReceiptNumber))
        {
            throw new GoodsReceiptManagementException(
                "Receipt number is required.");
        }

        if (request.ReceivedAtUtc == default)
        {
            throw new GoodsReceiptManagementException(
                "Received date is required.");
        }

        if (request.Lines is null ||
            request.Lines.Count == 0)
        {
            throw new GoodsReceiptManagementException(
                "A goods receipt must contain at least one line.");
        }

        var duplicateLines =
            request.Lines
                .GroupBy(
                    x => x.PurchaseOrderLineId)
                .Any(x => x.Count() > 1);

        if (duplicateLines)
        {
            throw new GoodsReceiptManagementException(
                "Duplicate purchase order lines were supplied.");
        }

        foreach (var line in request.Lines)
        {
            if (line.PurchaseOrderLineId ==
                Guid.Empty)
            {
                throw new GoodsReceiptManagementException(
                    "Purchase order line is required.");
            }

            if (line.ReceivedQuantity <= 0m)
            {
                throw new GoodsReceiptManagementException(
                    "Received quantity must be greater than zero.");
            }

            if (line.AcceptedQuantity < 0m ||
                line.RejectedQuantity < 0m)
            {
                throw new GoodsReceiptManagementException(
                    "Accepted and rejected quantities cannot be negative.");
            }

            if (line.AcceptedQuantity +
                    line.RejectedQuantity !=
                line.ReceivedQuantity)
            {
                throw new GoodsReceiptManagementException(
                    "Accepted quantity plus rejected quantity must equal received quantity.");
            }

            if (line.RejectedQuantity > 0m &&
                string.IsNullOrWhiteSpace(
                    line.RejectionReason))
            {
                throw new GoodsReceiptManagementException(
                    "A rejection reason is required when material is rejected.");
            }
        }
    }

    private IQueryable<GoodsReceipt> BuildQuery()
    {
        return _dbContext.GoodsReceipts
            .AsNoTracking()
            .Include(x => x.PurchaseOrder)
            .Include(x => x.ConstructionSite)
            .Include(x => x.Lines)
                .ThenInclude(x => x.Material)
            .Include(x => x.Lines)
                .ThenInclude(x => x.PurchaseOrderLine);
    }

    private async Task<GoodsReceipt> GetGoodsReceiptAsync(
        Guid goodsReceiptId,
        CancellationToken cancellationToken)
    {
        return await BuildQuery()
            .SingleOrDefaultAsync(
                x => x.Id == goodsReceiptId,
                cancellationToken)
            ?? throw new GoodsReceiptManagementException(
                "Goods receipt was not found.");
    }

    private async Task<GoodsReceiptResponse> GetResponseAsync(
        Guid goodsReceiptId,
        CancellationToken cancellationToken)
    {
        var record =
            await BuildQuery()
                .SingleOrDefaultAsync(
                    x => x.Id == goodsReceiptId,
                    cancellationToken)
            ?? throw new GoodsReceiptManagementException(
                "Goods receipt was not found.");

        var stockMovements =
            await _dbContext.StockMovements
                .AsNoTracking()
                .Include(x => x.StockItem)
                .Where(
                    x =>
                        x.ReferenceType == "GoodsReceipt" &&
                        x.ReferenceId == goodsReceiptId)
                .ToListAsync(cancellationToken);

        return Map(record, stockMovements);
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
            ?? throw new GoodsReceiptManagementException(
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

        throw new GoodsReceiptManagementException(
            "You are not authorized to manage goods receipts.");
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
            throw new GoodsReceiptManagementException(
                "You are not authorized to access this goods receipt.");
        }
    }

    private static Guid GetActorCompanyId(
        User actor)
    {
        if (!actor.CompanyId.HasValue)
        {
            throw new GoodsReceiptManagementException(
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

    private static string? Clean(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static GoodsReceiptResponse Map(
        GoodsReceipt record,
        IReadOnlyCollection<StockMovement> stockMovements)
    {
        return new GoodsReceiptResponse
        {
            Id =
                record.Id,
            PurchaseOrderId =
                record.PurchaseOrderId,
            PurchaseOrderNumber =
                record.PurchaseOrder.PurchaseOrderNumber,
            ConstructionSiteId =
                record.ConstructionSiteId,
            SiteName =
                record.ConstructionSite.Name,
            ReceiptNumber =
                record.ReceiptNumber,
            ReceivedAtUtc =
                record.ReceivedAtUtc,
            ReceivedBy =
                record.ReceivedBy,
            DeliveryNoteNumber =
                record.DeliveryNoteNumber,
            VehiclePlateNumber =
                record.VehiclePlateNumber,
            Remarks =
                record.Remarks,
            Lines =
                record.Lines
                    .OrderBy(
                        x => x.Material.MaterialCode)
                    .Select(
                        x =>
                        {
                            var movement =
                                stockMovements
                                    .FirstOrDefault(
                                        m =>
                                            m.StockItem.MaterialId ==
                                            x.MaterialId);

                            return new GoodsReceiptLineResponse
                            {
                                Id =
                                    x.Id,
                                PurchaseOrderLineId =
                                    x.PurchaseOrderLineId,
                                MaterialId =
                                    x.MaterialId,
                                MaterialCode =
                                    x.Material.MaterialCode,
                                MaterialName =
                                    x.Material.Name,
                                ReceivedQuantity = x.ReceivedQuantity,
                                OrderedQuantity = x.PurchaseOrderLine.OrderedQuantity,
                                RemainingQuantity =
                                    x.PurchaseOrderLine.OrderedQuantity -
                                    x.PurchaseOrderLine.ReceivedQuantity,

                                AcceptedQuantity = x.AcceptedQuantity,
                                RejectedQuantity = x.RejectedQuantity,
                                StockBalanceBefore =
                                    movement?.StockBalanceBefore,
                                StockBalanceAfter =
                                    movement?.StockBalanceAfter,
                                RejectionReason =
                                    x.RejectionReason,
                                Remarks =
                                    x.Remarks
                            };
                        })
                    .ToList()
        };
    }
}
