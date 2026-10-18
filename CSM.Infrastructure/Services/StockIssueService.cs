using CSM.Application.Common.Exceptions;
using CSM.Application.Inventory.StockIssues;
using CSM.Application.Inventory.StockIssues.Dtos;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Procurement;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using CSM.Application.Notifications;

namespace CSM.Infrastructure.Services;

public sealed class StockIssueService : IStockIssueService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILowStockNotificationService _lowStockNotificationService;

    public StockIssueService(
        ApplicationDbContext dbContext,
        ILowStockNotificationService lowStockNotificationService)
    {
        _dbContext = dbContext;
        _lowStockNotificationService = lowStockNotificationService;
    }

    public async Task<IReadOnlyCollection<StockIssueResponse>> GetAllAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(currentUserId, cancellationToken);

        EnsureCanManage(actor);

        var query = _dbContext.StockMovements
            .AsNoTracking()
            .Where(x => x.MovementType == StockMovementType.Issue)
            .Include(x => x.StockItem)
                .ThenInclude(x => x.ConstructionSite)
            .Include(x => x.StockItem)
                .ThenInclude(x => x.Material)
            .AsQueryable();

        if (!IsSuperAdmin(actor))
        {
            var companyId = GetActorCompanyId(actor);

            query = query.Where(x => x.CompanyId == companyId);
        }

        var records = await query
            .OrderByDescending(x => x.MovementDateUtc)
            .ToListAsync(cancellationToken);

        return records.Select(Map).ToList();
    }

    public async Task<StockIssueResponse> GetByIdAsync(
        Guid currentUserId,
        Guid stockMovementId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(currentUserId, cancellationToken);

        EnsureCanManage(actor);

        var query = _dbContext.StockMovements
            .AsNoTracking()
            .Where(x =>
                x.Id == stockMovementId &&
                x.MovementType == StockMovementType.Issue)
            .Include(x => x.StockItem)
                .ThenInclude(x => x.ConstructionSite)
            .Include(x => x.StockItem)
                .ThenInclude(x => x.Material)
            .AsQueryable();

        if (!IsSuperAdmin(actor))
        {
            var companyId = GetActorCompanyId(actor);

            query = query.Where(x => x.CompanyId == companyId);
        }

        var movement = await query.SingleOrDefaultAsync(cancellationToken)
            ?? throw new StockIssueManagementException("Stock issue was not found.");

        return Map(movement);
    }

    public async Task<StockIssueResponse> CreateAsync(
        Guid currentUserId,
        CreateStockIssueRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanManage(actor);

        ValidateRequest(request);

        var site = await _dbContext.ConstructionSites
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == request.ConstructionSiteId,
                cancellationToken)
            ?? throw new StockIssueManagementException(
                "Construction site was not found.");

        EnsureCompanyAccess(
            actor,
            site.CompanyId);

        var material = await _dbContext.Materials
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x =>
                    x.Id == request.MaterialId &&
                    x.CompanyId == site.CompanyId,
                cancellationToken)
            ?? throw new StockIssueManagementException(
                "Material was not found for this company.");

        var normalizedReferenceNumber =
            request.ReferenceNumber
                .Trim()
                .ToUpperInvariant();

        var duplicateReference =
            await _dbContext.StockMovements
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        x.CompanyId == site.CompanyId &&
                        x.MovementType ==
                            StockMovementType.Issue &&
                        x.ReferenceNumber ==
                            normalizedReferenceNumber,
                    cancellationToken);

        if (duplicateReference)
        {
            throw new StockIssueManagementException(
                "A stock issue with this reference number already exists in the company.");
        }

        var strategy =
            _dbContext.Database.CreateExecutionStrategy();

        Guid stockMovementId = Guid.Empty;
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
                    var stockItem =
                        await _dbContext.StockItems
                            .SingleOrDefaultAsync(
                                x =>
                                    x.CompanyId ==
                                        site.CompanyId &&
                                    x.ConstructionSiteId ==
                                        site.Id &&
                                    x.MaterialId ==
                                        material.Id,
                                cancellationToken)
                        ?? throw new StockIssueManagementException(
                            "Stock item was not found for the selected site and material.");

                    var availableQuantity =
                        stockItem.QuantityOnHand -
                        stockItem.QuantityReserved;

                    if (request.Quantity >
                        availableQuantity)
                    {
                        throw new StockIssueManagementException(
                            "Issue quantity exceeds the available stock quantity.");
                    }

                    quantityBefore =
                        stockItem.QuantityOnHand;

                    unitCost =
                        stockItem.AverageUnitCost;

                    stockItem.QuantityOnHand -=
                        request.Quantity;

                    quantityAfter =
                        stockItem.QuantityOnHand;

                    var movement = new StockMovement
                    {
                        CompanyId = stockItem.CompanyId,
                        StockItemId = stockItem.Id,

                        MovementType = StockMovementType.Issue,
                        MovementDateUtc = request.IssueDateUtc,

                        Quantity = request.Quantity,

                        // Save audit balances
                        StockBalanceBefore = quantityBefore,
                        StockBalanceAfter = quantityAfter,

                        UnitCost = unitCost,

                        ReferenceType = "StockIssue",
                        ReferenceNumber = normalizedReferenceNumber,

                        Remarks = BuildRemarks(request)
                    };

                    _dbContext.StockMovements.Add(movement);

                    await _dbContext.SaveChangesAsync(cancellationToken);

                    await _lowStockNotificationService.NotifyIfLowStockAsync(
                        stockItem.Id,
                        currentUserId,
                        cancellationToken);

                    stockMovementId = movement.Id;

                    await transaction.CommitAsync(cancellationToken);
                }
                catch
                {
                    await transaction.RollbackAsync(
                        cancellationToken);

                    throw;
                }
            });

        return new StockIssueResponse
        {
            StockMovementId =
                stockMovementId,

            StockItemId =
                await GetStockItemIdAsync(
                    site.Id,
                    material.Id,
                    cancellationToken),

            ConstructionSiteId =
                site.Id,

            SiteName =
                site.Name,

            MaterialId =
                material.Id,

            MaterialCode =
                material.MaterialCode,

            MaterialName =
                material.Name,

            UnitOfMeasure =
                material.UnitOfMeasure,

            QuantityIssued =
                request.Quantity,

            QuantityOnHandBefore =
                quantityBefore,

            QuantityOnHandAfter =
                quantityAfter,

            UnitCost =
                unitCost,

            TotalCost =
                request.Quantity * unitCost,

            IssueDateUtc =
                request.IssueDateUtc,

            ReferenceNumber =
                normalizedReferenceNumber,

            IssuedTo =
                Clean(request.IssuedTo),

            Purpose =
                Clean(request.Purpose),

            Remarks =
                Clean(request.Remarks)
        };
    }

    private static StockIssueResponse Map(StockMovement movement)
    {
        var remarks = movement.Remarks ?? string.Empty;

        string? issuedTo = null;
        string? purpose = null;
        string? note = null;

        foreach (var part in remarks.Split(" | ", StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.StartsWith("Issued To: "))
            {
                issuedTo = part["Issued To: ".Length..];
            }
            else if (part.StartsWith("Purpose: "))
            {
                purpose = part["Purpose: ".Length..];
            }
            else
            {
                note = part;
            }
        }

        return new StockIssueResponse
        {
            StockMovementId = movement.Id,
            StockItemId = movement.StockItemId,
            ConstructionSiteId = movement.StockItem.ConstructionSiteId,
            SiteName = movement.StockItem.ConstructionSite.Name,
            MaterialId = movement.StockItem.MaterialId,
            MaterialCode = movement.StockItem.Material.MaterialCode,
            MaterialName = movement.StockItem.Material.Name,
            UnitOfMeasure = movement.StockItem.Material.UnitOfMeasure,
            QuantityIssued = movement.Quantity,
            QuantityOnHandBefore = movement.StockBalanceBefore ?? 0m,
            QuantityOnHandAfter  = movement.StockBalanceAfter ?? 0m,
            UnitCost = movement.UnitCost ?? 0m,
            TotalCost = movement.Quantity * (movement.UnitCost ?? 0m),
            IssueDateUtc = movement.MovementDateUtc,
            ReferenceNumber = movement.ReferenceNumber ?? string.Empty,
            IssuedTo = issuedTo,
            Purpose = purpose,
            Remarks = note
        };
    }

    private async Task<Guid> GetStockItemIdAsync(
        Guid constructionSiteId,
        Guid materialId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.StockItems
            .AsNoTracking()
            .Where(
                x =>
                    x.ConstructionSiteId ==
                        constructionSiteId &&
                    x.MaterialId ==
                        materialId)
            .Select(x => x.Id)
            .SingleAsync(cancellationToken);
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
            ?? throw new StockIssueManagementException(
                "Current user was not found.");
    }

    private static void ValidateRequest(
        CreateStockIssueRequest request)
    {
        if (request.ConstructionSiteId ==
            Guid.Empty)
        {
            throw new StockIssueManagementException(
                "Construction site is required.");
        }

        if (request.MaterialId ==
            Guid.Empty)
        {
            throw new StockIssueManagementException(
                "Material is required.");
        }

        if (request.Quantity <= 0m)
        {
            throw new StockIssueManagementException(
                "Issue quantity must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(
                request.ReferenceNumber))
        {
            throw new StockIssueManagementException(
                "Reference number is required.");
        }

        if (request.IssueDateUtc ==
            default)
        {
            throw new StockIssueManagementException(
                "Issue date is required.");
        }
    }

    private static void EnsureCanManage(
        User actor)
    {
        if (IsSuperAdmin(actor) ||
            HasRole(
                actor,
                AppRoles.Storekeeper) ||
            HasRole(
                actor,
                AppRoles.ProcurementOfficer))
        {
            return;
        }

        throw new StockIssueManagementException(
            "You are not authorized to manage stock issues.");
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
            throw new StockIssueManagementException(
                "You are not authorized to access this company stock.");
        }
    }

    private static Guid GetActorCompanyId(
        User actor)
    {
        if (!actor.CompanyId.HasValue)
        {
            throw new StockIssueManagementException(
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

    private static string? Clean(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static string? BuildRemarks(
        CreateStockIssueRequest request)
    {
        var parts = new List<string>();

        var issuedTo =
            Clean(request.IssuedTo);

        var purpose =
            Clean(request.Purpose);

        var remarks =
            Clean(request.Remarks);

        if (issuedTo is not null)
        {
            parts.Add(
                $"Issued To: {issuedTo}");
        }

        if (purpose is not null)
        {
            parts.Add(
                $"Purpose: {purpose}");
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

