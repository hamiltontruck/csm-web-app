using CSM.Application.Common.Exceptions;
using CSM.Application.Procurement.PurchaseOrders;
using CSM.Application.Procurement.PurchaseOrders.Dtos;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Procurement;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public sealed class PurchaseOrderService : IPurchaseOrderService
{
    private readonly ApplicationDbContext _dbContext;

    public PurchaseOrderService(
        ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<PurchaseOrderResponse>> GetAllAsync(
        Guid currentUserId,
        Guid? constructionSiteId,
        Guid? vendorId,
        PurchaseOrderStatus? status,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanManage(actor);

        var query = BuildQuery()
            .AsSplitQuery();

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

        if (vendorId.HasValue)
        {
            query = query.Where(
                x => x.VendorId == vendorId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(
                x => x.Status == status.Value);
        }

        var records = await query
            .OrderByDescending(x => x.OrderDate)
            .ThenByDescending(x => x.PurchaseOrderNumber)
            .ToListAsync(cancellationToken);

        return records
            .Select(Map)
            .ToList();
    }

    public async Task<PurchaseOrderResponse> GetByIdAsync(
        Guid currentUserId,
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanManage(actor);

        var record = await GetPurchaseOrderAsync(
            purchaseOrderId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            record.CompanyId);

        return Map(record);
    }

    public async Task<PurchaseOrderResponse> CreateAsync(
        Guid currentUserId,
        CreatePurchaseOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanManage(actor);

        ValidateHeader(
            request.PurchaseOrderNumber,
            request.OrderDate,
            request.ExpectedDeliveryDate,
            request.CurrencyCode,
            request.ExchangeRate,
            request.DiscountAmount);

        ValidateLines(request.Lines);

        var site = await _dbContext.ConstructionSites
            .SingleOrDefaultAsync(
                x => x.Id == request.ConstructionSiteId,
                cancellationToken)
            ?? throw new PurchaseOrderManagementException(
                "Construction site was not found.");

        EnsureCompanyAccess(
            actor,
            site.CompanyId);

        await ValidateProjectAsync(
            site.CompanyId,
            site.Id,
            request.ProjectId,
            cancellationToken);

        await ValidateVendorAsync(
            site.CompanyId,
            request.VendorId,
            cancellationToken);

        await ValidateMaterialsAndCostCodesAsync(
            site.CompanyId,
            request.Lines,
            cancellationToken);

        await ValidateMaterialRequestAsync(
            site.CompanyId,
            site.Id,
            request.ProjectId,
            request.MaterialRequestId,
            request.Lines,
            null,
            cancellationToken);

        var normalizedNumber =
            request.PurchaseOrderNumber
                .Trim()
                .ToUpperInvariant();

        var duplicate =
            await _dbContext.PurchaseOrders.AnyAsync(
                x =>
                    x.CompanyId == site.CompanyId &&
                    x.PurchaseOrderNumber.ToUpper() ==
                    normalizedNumber,
                cancellationToken);

        if (duplicate)
        {
            throw new PurchaseOrderManagementException(
                "A purchase order with this purchase order number already exists in the company.");
        }

        var record = new PurchaseOrder
        {
            CompanyId = site.CompanyId,
            ConstructionSiteId = site.Id,
            ProjectId = request.ProjectId,
            VendorId = request.VendorId,
            MaterialRequestId =
                request.MaterialRequestId,
            PurchaseOrderNumber =
                normalizedNumber,
            OrderDate = request.OrderDate,
            ExpectedDeliveryDate =
                request.ExpectedDeliveryDate,
            CurrencyCode =
                request.CurrencyCode
                    .Trim()
                    .ToUpperInvariant(),
            ExchangeRate = request.ExchangeRate,
            DiscountAmount =
                request.DiscountAmount,
            Status = PurchaseOrderStatus.Draft,
            DeliveryAddress =
                Clean(request.DeliveryAddress),
            PaymentTerms =
                Clean(request.PaymentTerms),
            Notes =
                Clean(request.Notes),
            CreatedBy = currentUserId
        };

        foreach (var input in request.Lines)
        {
            record.Lines.Add(
                CreateLine(
                    site.CompanyId,
                    currentUserId,
                    input));
        }

        CalculateTotals(record);

        _dbContext.PurchaseOrders.Add(record);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return await GetResponseAsync(
            record.Id,
            cancellationToken);
    }

    public async Task<PurchaseOrderResponse> UpdateAsync(
        Guid currentUserId,
        Guid purchaseOrderId,
        UpdatePurchaseOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanManage(actor);

        var record = await GetPurchaseOrderAsync(
            purchaseOrderId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            record.CompanyId);

        if (record.Status != PurchaseOrderStatus.Draft)
        {
            throw new PurchaseOrderManagementException(
                "Only Draft purchase orders can be updated.");
        }

        ValidateHeader(
            request.PurchaseOrderNumber,
            request.OrderDate,
            request.ExpectedDeliveryDate,
            request.CurrencyCode,
            request.ExchangeRate,
            request.DiscountAmount);

        ValidateLines(request.Lines);

        var site = await _dbContext.ConstructionSites
            .SingleOrDefaultAsync(
                x => x.Id == request.ConstructionSiteId,
                cancellationToken)
            ?? throw new PurchaseOrderManagementException(
                "Construction site was not found.");

        if (site.CompanyId != record.CompanyId)
        {
            throw new PurchaseOrderManagementException(
                "The selected construction site does not belong to this company.");
        }

        await ValidateProjectAsync(
            record.CompanyId,
            site.Id,
            request.ProjectId,
            cancellationToken);

        await ValidateVendorAsync(
            record.CompanyId,
            request.VendorId,
            cancellationToken);

        await ValidateMaterialsAndCostCodesAsync(
            record.CompanyId,
            request.Lines,
            cancellationToken);

        await ValidateMaterialRequestAsync(
            record.CompanyId,
            site.Id,
            request.ProjectId,
            request.MaterialRequestId,
            request.Lines,
            record.Id,
            cancellationToken);

        var normalizedNumber =
            request.PurchaseOrderNumber
                .Trim()
                .ToUpperInvariant();

        var duplicate =
            await _dbContext.PurchaseOrders.AnyAsync(
                x =>
                    x.Id != record.Id &&
                    x.CompanyId == record.CompanyId &&
                    x.PurchaseOrderNumber.ToUpper() ==
                    normalizedNumber,
                cancellationToken);

        if (duplicate)
        {
            throw new PurchaseOrderManagementException(
                "A purchase order with this purchase order number already exists in the company.");
        }

        record.ConstructionSiteId = site.Id;
        record.ProjectId = request.ProjectId;
        record.VendorId = request.VendorId;
        record.MaterialRequestId =
            request.MaterialRequestId;
        record.PurchaseOrderNumber =
            normalizedNumber;
        record.OrderDate = request.OrderDate;
        record.ExpectedDeliveryDate =
            request.ExpectedDeliveryDate;
        record.CurrencyCode =
            request.CurrencyCode
                .Trim()
                .ToUpperInvariant();
        record.ExchangeRate =
            request.ExchangeRate;
        record.DiscountAmount =
            request.DiscountAmount;
        record.DeliveryAddress =
            Clean(request.DeliveryAddress);
        record.PaymentTerms =
            Clean(request.PaymentTerms);
        record.Notes =
            Clean(request.Notes);
        record.UpdatedBy = currentUserId;

        // Update existing lines in place where possible.
        // This avoids severing required relationships and the
        // soft-delete replacement problems previously encountered
        // with MaterialRequestLine.
        var existingByMaterial = record.Lines
            .ToDictionary(x => x.MaterialId);

        var requestedMaterialIds = request.Lines
            .Select(x => x.MaterialId)
            .ToHashSet();


        // A Draft PO has no receipts, so lines absent from the new
        // request can safely be soft-deleted explicitly.
        foreach (var existing in record.Lines.ToList())
        {
            if (requestedMaterialIds.Contains(
                    existing.MaterialId))
            {
                continue;
            }

            existing.IsDeleted = true;
            existing.DeletedAtUtc = DateTime.UtcNow;
            existing.DeletedBy = currentUserId;
            existing.UpdatedBy = currentUserId;
        }

        foreach (var input in request.Lines)
        {
            if (existingByMaterial.TryGetValue(
                    input.MaterialId,
                    out var existing))
            {
                existing.CostCodeId =
                    input.CostCodeId;
                existing.OrderedQuantity =
                    input.OrderedQuantity;
                existing.UnitPrice =
                    input.UnitPrice;
                existing.TaxAmount =
                    input.TaxAmount;
                existing.LineTotal =
                    CalculateLineTotal(input);
                existing.UpdatedBy =
                    currentUserId;
            }
            else
            {
                record.Lines.Add(
                    CreateLine(
                        record.CompanyId,
                        currentUserId,
                        input));
            }
        }

        CalculateTotals(record);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return await GetResponseAsync(
            record.Id,
            cancellationToken);
    }

    public async Task<PurchaseOrderResponse> SubmitAsync(
        Guid currentUserId,
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanManage(actor);

        var record = await GetPurchaseOrderAsync(
            purchaseOrderId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            record.CompanyId);

        if (record.Status != PurchaseOrderStatus.Draft)
        {
            throw new PurchaseOrderManagementException(
                "Only Draft purchase orders can be submitted.");
        }

        if (record.Lines.Count == 0)
        {
            throw new PurchaseOrderManagementException(
                "A purchase order must contain at least one line before submission.");
        }

        record.Status =
            PurchaseOrderStatus.PendingApproval;
        record.UpdatedBy =
            currentUserId;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(record);
    }

    public async Task<PurchaseOrderResponse> ApproveAsync(
        Guid currentUserId,
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanApprove(actor);

        var record = await GetPurchaseOrderAsync(
            purchaseOrderId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            record.CompanyId);

        if (record.Status !=
            PurchaseOrderStatus.PendingApproval)
        {
            throw new PurchaseOrderManagementException(
                "Only PendingApproval purchase orders can be approved.");
        }

        record.Status =
            PurchaseOrderStatus.Approved;
        record.ApprovedBy =
            currentUserId;
        record.ApprovedAtUtc =
            DateTime.UtcNow;
        record.UpdatedBy =
            currentUserId;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(record);
    }

    public async Task<PurchaseOrderResponse> SendToVendorAsync(
        Guid currentUserId,
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanManage(actor);

        var record = await GetPurchaseOrderAsync(
            purchaseOrderId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            record.CompanyId);

        if (record.Status != PurchaseOrderStatus.Approved)
        {
            throw new PurchaseOrderManagementException(
                "Only Approved purchase orders can be sent to the vendor.");
        }

        record.Status =
            PurchaseOrderStatus.SentToVendor;
        record.UpdatedBy =
            currentUserId;

        if (record.MaterialRequestId.HasValue)
        {
            var materialRequest =
                await _dbContext.MaterialRequests
                    .SingleOrDefaultAsync(
                        x => x.Id == record.MaterialRequestId.Value,
                        cancellationToken)
                ?? throw new PurchaseOrderManagementException(
                    "The purchase order is linked to a material request that was not found.");

            if (materialRequest.Status is
                MaterialRequestStatus.Approved or
                MaterialRequestStatus.PartiallyApproved)
            {
                materialRequest.Status =
                    MaterialRequestStatus.Ordered;

                materialRequest.UpdatedBy =
                    currentUserId;
            }
        }

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(record);
    }

    public async Task<PurchaseOrderResponse> CancelAsync(
        Guid currentUserId,
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanManage(actor);

        var record = await GetPurchaseOrderAsync(
            purchaseOrderId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            record.CompanyId);

        if (record.Status is
            PurchaseOrderStatus.PartiallyDelivered or
            PurchaseOrderStatus.Delivered or
            PurchaseOrderStatus.Closed)
        {
            throw new PurchaseOrderManagementException(
                "A purchase order cannot be cancelled after delivery has started.");
        }

        if (record.Status ==
            PurchaseOrderStatus.Cancelled)
        {
            throw new PurchaseOrderManagementException(
                "Purchase order is already cancelled.");
        }

        var hasGoodsReceipts =
            await _dbContext.GoodsReceipts.AnyAsync(
                x => x.PurchaseOrderId == record.Id,
                cancellationToken);

        if (hasGoodsReceipts)
        {
            throw new PurchaseOrderManagementException(
                "A purchase order linked to a goods receipt cannot be cancelled.");
        }

        record.Status =
            PurchaseOrderStatus.Cancelled;
        record.UpdatedBy =
            currentUserId;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(record);
    }

    private IQueryable<PurchaseOrder> BuildQuery()
    {
        return _dbContext.PurchaseOrders
            .AsNoTracking()
            .AsSplitQuery()   // ← Add only this line
            .Include(x => x.ConstructionSite)
            .Include(x => x.Project)
            .Include(x => x.Vendor)
            .Include(x => x.MaterialRequest)
            .Include(x => x.Lines)
                .ThenInclude(x => x.Material)
            .Include(x => x.Lines)
                .ThenInclude(x => x.CostCode);
    }

    private async Task<PurchaseOrder> GetPurchaseOrderAsync(
        Guid purchaseOrderId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.PurchaseOrders
            .Include(x => x.ConstructionSite)
            .Include(x => x.Project)
            .Include(x => x.Vendor)
            .Include(x => x.MaterialRequest)
            .Include(x => x.Lines)
                .ThenInclude(x => x.Material)
            .Include(x => x.Lines)
                .ThenInclude(x => x.CostCode)
            .SingleOrDefaultAsync(
                x => x.Id == purchaseOrderId,
                cancellationToken)
            ?? throw new PurchaseOrderManagementException(
                "Purchase order was not found.");
    }

    private async Task<PurchaseOrderResponse> GetResponseAsync(
        Guid purchaseOrderId,
        CancellationToken cancellationToken)
    {
        var record = await BuildQuery()
            .SingleOrDefaultAsync(
                x => x.Id == purchaseOrderId,
                cancellationToken)
            ?? throw new PurchaseOrderManagementException(
                "Purchase order was not found.");

        return Map(record);
    }

    private async Task ValidateProjectAsync(
        Guid companyId,
        Guid constructionSiteId,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        if (!projectId.HasValue)
        {
            return;
        }

        var project = await _dbContext.Projects
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == projectId.Value,
                cancellationToken)
            ?? throw new PurchaseOrderManagementException(
                "Project was not found.");

        if (project.CompanyId != companyId)
        {
            throw new PurchaseOrderManagementException(
                "The selected project does not belong to this company.");
        }

        if (project.ConstructionSiteId !=
            constructionSiteId)
        {
            throw new PurchaseOrderManagementException(
                "The selected project does not belong to the selected construction site.");
        }
    }

    private async Task ValidateVendorAsync(
        Guid companyId,
        Guid vendorId,
        CancellationToken cancellationToken)
    {
        var vendor = await _dbContext.Vendors
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == vendorId,
                cancellationToken)
            ?? throw new PurchaseOrderManagementException(
                "Vendor was not found.");

        if (vendor.CompanyId != companyId)
        {
            throw new PurchaseOrderManagementException(
                "The selected vendor does not belong to this company.");
        }

        if (!vendor.IsActive)
        {
            throw new PurchaseOrderManagementException(
                "The selected vendor is inactive.");
        }
    }

    private async Task ValidateMaterialsAndCostCodesAsync(
        Guid companyId,
        IReadOnlyCollection<PurchaseOrderLineRequest> lines,
        CancellationToken cancellationToken)
    {
        var materialIds = lines
            .Select(x => x.MaterialId)
            .Distinct()
            .ToList();

        var materials = await _dbContext.Materials
            .AsNoTracking()
            .Where(x => materialIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        if (materials.Count != materialIds.Count)
        {
            throw new PurchaseOrderManagementException(
                "One or more selected materials were not found.");
        }

        if (materials.Any(
                x => x.CompanyId != companyId))
        {
            throw new PurchaseOrderManagementException(
                "One or more selected materials do not belong to this company.");
        }

        if (materials.Any(x => !x.IsActive))
        {
            throw new PurchaseOrderManagementException(
                "One or more selected materials are inactive.");
        }

        var costCodeIds = lines
            .Where(x => x.CostCodeId.HasValue)
            .Select(x => x.CostCodeId!.Value)
            .Distinct()
            .ToList();

        if (costCodeIds.Count == 0)
        {
            return;
        }

        var costCodes = await _dbContext.CostCodes
            .AsNoTracking()
            .Where(x => costCodeIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        if (costCodes.Count != costCodeIds.Count)
        {
            throw new PurchaseOrderManagementException(
                "One or more selected cost codes were not found.");
        }

        if (costCodes.Any(
                x => x.CompanyId != companyId))
        {
            throw new PurchaseOrderManagementException(
                "One or more selected cost codes do not belong to this company.");
        }

        if (costCodes.Any(x => !x.IsActive))
        {
            throw new PurchaseOrderManagementException(
                "One or more selected cost codes are inactive.");
        }
    }

    private async Task ValidateMaterialRequestAsync(
        Guid companyId,
        Guid constructionSiteId,
        Guid? projectId,
        Guid? materialRequestId,
        IReadOnlyCollection<PurchaseOrderLineRequest> lines,
        Guid? currentPurchaseOrderId,
        CancellationToken cancellationToken)
    {
        if (!materialRequestId.HasValue)
        {
            return;
        }

        var materialRequest =
            await _dbContext.MaterialRequests
                .AsNoTracking()
                .Include(x => x.Lines)
                .SingleOrDefaultAsync(
                    x => x.Id == materialRequestId.Value,
                    cancellationToken)
            ?? throw new PurchaseOrderManagementException(
                "Material request was not found.");

        if (materialRequest.CompanyId != companyId)
        {
            throw new PurchaseOrderManagementException(
                "The selected material request does not belong to this company.");
        }

        if (materialRequest.ConstructionSiteId !=
            constructionSiteId)
        {
            throw new PurchaseOrderManagementException(
                "The selected material request does not belong to the selected construction site.");
        }

        if (materialRequest.ProjectId != projectId)
        {
            throw new PurchaseOrderManagementException(
                "The selected material request does not match the selected project.");
        }

        if (materialRequest.Status is not
            MaterialRequestStatus.Approved and not
            MaterialRequestStatus.PartiallyApproved and not
            MaterialRequestStatus.Ordered and not
            MaterialRequestStatus.PartiallyDelivered)
        {
            throw new PurchaseOrderManagementException(
                "Only approved material requests can be used for purchase orders.");
        }

        foreach (var input in lines)
        {
            var requestLine =
                materialRequest.Lines.SingleOrDefault(
                    x => x.MaterialId == input.MaterialId);

            if (requestLine is null)
            {
                throw new PurchaseOrderManagementException(
                    "A purchase order material is not present on the selected material request.");
            }

            var outstandingOnOtherPurchaseOrders =
                await _dbContext.PurchaseOrderLines
                    .AsNoTracking()
                    .Where(
                        x =>
                            x.PurchaseOrder.MaterialRequestId ==
                                materialRequest.Id &&
                            x.MaterialId == input.MaterialId &&
                            x.PurchaseOrder.Status !=
                                PurchaseOrderStatus.Cancelled &&
                            x.PurchaseOrder.Status !=
                                PurchaseOrderStatus.Delivered &&
                            x.PurchaseOrder.Status !=
                                PurchaseOrderStatus.Closed &&
                            (!currentPurchaseOrderId.HasValue ||
                             x.PurchaseOrderId !=
                                currentPurchaseOrderId.Value))
                    .SumAsync(
                        x =>
                            (decimal?)(
                                x.OrderedQuantity -
                                x.ReceivedQuantity),
                        cancellationToken)
                ?? 0m;

            var available =
                requestLine.ApprovedQuantity -
                requestLine.DeliveredQuantity -
                outstandingOnOtherPurchaseOrders;

            if (available < 0m)
            {
                available = 0m;
            }

            if (input.OrderedQuantity > available)
            {
                throw new PurchaseOrderManagementException(
                    $"Ordered quantity for material {input.MaterialId} exceeds the remaining approved material request quantity.");
            }
        }
    }

    private static void ValidateHeader(
        string purchaseOrderNumber,
        DateOnly orderDate,
        DateOnly? expectedDeliveryDate,
        string currencyCode,
        decimal exchangeRate,
        decimal discountAmount)
    {
        if (string.IsNullOrWhiteSpace(
                purchaseOrderNumber))
        {
            throw new PurchaseOrderManagementException(
                "Purchase order number is required.");
        }

        if (purchaseOrderNumber.Trim().Length > 50)
        {
            throw new PurchaseOrderManagementException(
                "Purchase order number cannot exceed 50 characters.");
        }

        if (orderDate == default)
        {
            throw new PurchaseOrderManagementException(
                "Order date is required.");
        }

        if (expectedDeliveryDate.HasValue &&
            expectedDeliveryDate.Value < orderDate)
        {
            throw new PurchaseOrderManagementException(
                "Expected delivery date cannot be before the order date.");
        }

        if (string.IsNullOrWhiteSpace(currencyCode) ||
            currencyCode.Trim().Length != 3)
        {
            throw new PurchaseOrderManagementException(
                "Currency code must contain exactly 3 characters.");
        }

        if (exchangeRate <= 0m)
        {
            throw new PurchaseOrderManagementException(
                "Exchange rate must be greater than zero.");
        }

        if (discountAmount < 0m)
        {
            throw new PurchaseOrderManagementException(
                "Discount amount cannot be negative.");
        }
    }

    private static void ValidateLines(
        IReadOnlyCollection<PurchaseOrderLineRequest> lines)
    {
        if (lines.Count == 0)
        {
            throw new PurchaseOrderManagementException(
                "A purchase order must contain at least one line.");
        }

        var duplicateMaterial =
            lines.GroupBy(x => x.MaterialId)
                .Any(x => x.Count() > 1);

        if (duplicateMaterial)
        {
            throw new PurchaseOrderManagementException(
                "Duplicate materials are not allowed on a purchase order.");
        }

        foreach (var line in lines)
        {
            if (line.MaterialId == Guid.Empty)
            {
                throw new PurchaseOrderManagementException(
                    "Material is required for every purchase order line.");
            }

            if (line.OrderedQuantity <= 0m)
            {
                throw new PurchaseOrderManagementException(
                    "Ordered quantity must be greater than zero.");
            }

            if (line.UnitPrice < 0m)
            {
                throw new PurchaseOrderManagementException(
                    "Unit price cannot be negative.");
            }

            if (line.TaxAmount < 0m)
            {
                throw new PurchaseOrderManagementException(
                    "Tax amount cannot be negative.");
            }
        }
    }

    private static PurchaseOrderLine CreateLine(
        Guid companyId,
        Guid currentUserId,
        PurchaseOrderLineRequest input)
    {
        return new PurchaseOrderLine
        {
            CompanyId = companyId,
            MaterialId = input.MaterialId,
            CostCodeId = input.CostCodeId,
            OrderedQuantity =
                input.OrderedQuantity,
            ReceivedQuantity = 0m,
            UnitPrice = input.UnitPrice,
            TaxAmount = input.TaxAmount,
            LineTotal =
                CalculateLineTotal(input),
            CreatedBy = currentUserId
        };
    }

    private static decimal CalculateLineTotal(
        PurchaseOrderLineRequest input)
    {
        return
            (input.OrderedQuantity *
             input.UnitPrice) +
            input.TaxAmount;
    }

    private static void CalculateTotals(
        PurchaseOrder record)
    {
        var activeLines = record.Lines
            .Where(x => !x.IsDeleted)
            .ToList();

        record.Subtotal = activeLines.Sum(
            x => x.OrderedQuantity * x.UnitPrice);

        record.TaxAmount = activeLines.Sum(
            x => x.TaxAmount);

        record.TotalAmount =
            record.Subtotal +
            record.TaxAmount -
            record.DiscountAmount;

        if (record.TotalAmount < 0m)
        {
            throw new PurchaseOrderManagementException(
                "Discount amount cannot exceed the purchase order subtotal plus tax.");
        }
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
            ?? throw new PurchaseOrderManagementException(
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

        throw new PurchaseOrderManagementException(
            "You are not authorized to manage purchase orders.");
    }

    private static void EnsureCanApprove(
        User actor)
    {
        if (IsSuperAdmin(actor) ||
            HasRole(actor, AppRoles.CompanyAdmin))
        {
            return;
        }

        throw new PurchaseOrderManagementException(
            "You are not authorized to approve purchase orders.");
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
            throw new PurchaseOrderManagementException(
                "You are not authorized to access this purchase order.");
        }
    }

    private static Guid GetActorCompanyId(
        User actor)
    {
        if (!actor.CompanyId.HasValue)
        {
            throw new PurchaseOrderManagementException(
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

    private static PurchaseOrderResponse Map(
        PurchaseOrder record)
    {
        return new PurchaseOrderResponse
        {
            Id = record.Id,
            CompanyId = record.CompanyId,
            ConstructionSiteId =
                record.ConstructionSiteId,
            SiteName =
                record.ConstructionSite.Name,
            ProjectId =
                record.ProjectId,
            ProjectName =
                record.Project?.Name,
            VendorId =
                record.VendorId,
            VendorCode =
                record.Vendor.VendorCode,
            VendorName =
                record.Vendor.Name,
            MaterialRequestId =
                record.MaterialRequestId,
            MaterialRequestNumber =
                record.MaterialRequest?.RequestNumber,
            PurchaseOrderNumber =
                record.PurchaseOrderNumber,
            OrderDate =
                record.OrderDate,
            ExpectedDeliveryDate =
                record.ExpectedDeliveryDate,
            CurrencyCode =
                record.CurrencyCode,
            ExchangeRate =
                record.ExchangeRate,
            Subtotal =
                record.Subtotal,
            TaxAmount =
                record.TaxAmount,
            DiscountAmount =
                record.DiscountAmount,
            TotalAmount =
                record.TotalAmount,
            Status =
                record.Status,
            DeliveryAddress =
                record.DeliveryAddress,
            PaymentTerms =
                record.PaymentTerms,
            Notes =
                record.Notes,
            ApprovedBy =
                record.ApprovedBy,
            ApprovedAtUtc =
                record.ApprovedAtUtc,
            CreatedAtUtc =
                record.CreatedAtUtc,
            UpdatedAtUtc =
                record.UpdatedAtUtc,
            Lines = record.Lines
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.Material.MaterialCode)
                .Select(
                    x => new PurchaseOrderLineResponse
                    {
                        Id = x.Id,
                        MaterialId = x.MaterialId,
                        MaterialCode =
                            x.Material.MaterialCode,
                        MaterialName =
                            x.Material.Name,
                        UnitOfMeasure =
                            x.Material.UnitOfMeasure,
                        CostCodeId =
                            x.CostCodeId,
                        CostCode =
                            x.CostCode?.Code,
                        OrderedQuantity =
                            x.OrderedQuantity,
                        ReceivedQuantity =
                            x.ReceivedQuantity,
                        UnitPrice =
                            x.UnitPrice,
                        TaxAmount =
                            x.TaxAmount,
                        LineTotal =
                            x.LineTotal
                    })
                .ToList()
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

