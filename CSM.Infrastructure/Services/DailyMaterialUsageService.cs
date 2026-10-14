using CSM.Application.Common.Exceptions;
using CSM.Application.Inventory.DailyMaterialUsage;
using CSM.Application.Inventory.DailyMaterialUsage.Dtos;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Procurement;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public sealed class DailyMaterialUsageService :
    IDailyMaterialUsageService
{
    private readonly ApplicationDbContext _dbContext;

    public DailyMaterialUsageService(
        ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<DailyMaterialUsageResponse>> GetAllAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        var query = _dbContext.DailyMaterialUsages
            .AsNoTracking()
            .Include(x => x.ConstructionSite)
            .Include(x => x.Material)
            .Include(x => x.DailyProgressLog)
            .Include(x => x.LoggedByUser)
            .AsQueryable();

        if (!IsSuperAdmin(actor))
        {
            query = query.Where(x =>
                x.CompanyId == actor.CompanyId);
        }

        var records = await query
            .OrderByDescending(x => x.Date)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return records
            .Select(Map)
            .ToArray();
    }

    public async Task<DailyMaterialUsageResponse> CreateAsync(
        Guid currentUserId,
        CreateDailyMaterialUsageRequest request,
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
            ?? throw new DailyMaterialUsageManagementException(
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
            ?? throw new DailyMaterialUsageManagementException(
                "Material was not found for this company.");

        var progressLog =
            await _dbContext.DailyProgressLogs
                .Include(x => x.Project)
                .SingleOrDefaultAsync(
                    x =>
                        x.Id ==
                            request.DailyProgressLogId,
                    cancellationToken)
            ?? throw new DailyMaterialUsageManagementException(
                "Daily progress log was not found.");

        if (progressLog.Project.ConstructionSiteId !=
            site.Id)
        {
            throw new DailyMaterialUsageManagementException(
                "Daily progress log does not belong to the selected construction site.");
        }

        if (progressLog.CompanyId != site.CompanyId)
        {
            throw new DailyMaterialUsageManagementException(
                "Daily progress log does not belong to this company.");
        }

        if (request.Date != progressLog.LogDate)
        {
            throw new DailyMaterialUsageManagementException(
                "Usage date must match the daily progress log date.");
        }

        var strategy =
            _dbContext.Database.CreateExecutionStrategy();

        Guid usageId = Guid.Empty;

        await strategy.ExecuteAsync(
            async () =>
            {
                await using var transaction =
                    await _dbContext.Database
                        .BeginTransactionAsync(
                            System.Data.IsolationLevel.Serializable,
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
                        ?? throw new DailyMaterialUsageManagementException(
                            "Stock item was not found for the selected site and material.");
                    stockItem.QuantityOnHand -=
                        request.QuantityUsed;

                    stockItem.QuantityOnHand +=
                        request.QuantityReturned;

                    if (stockItem.QuantityOnHand < 0m)
                    {
                        throw new DailyMaterialUsageManagementException(
                            "Daily material usage would result in negative stock.");
                    }

                    var quantityVariance =
                        request.QuantityIssued -
                        request.QuantityUsed -
                        request.QuantityReturned;

                    var requiresSupervisorReview =
                        quantityVariance != 0m;

                    var usageNumber =
                        await GetNextUsageNumberAsync(
                            site.CompanyId,
                            cancellationToken);

                    var usage =
                        new DailyMaterialUsage
                        {
                            CompanyId =
                                site.CompanyId,

                            UsageNumber =
                                usageNumber,

                            ConstructionSiteId =
                                site.Id,

                            MaterialId =
                                material.Id,

                            DailyProgressLogId =
                                progressLog.Id,

                            LoggedByUserId =
                                currentUserId,

                            Date =
                                request.Date,

                            QuantityIssued =
                                request.QuantityIssued,

                            QuantityUsed =
                                request.QuantityUsed,

                            QuantityReturned =
                                request.QuantityReturned,

                            QuantityVariance =
                                quantityVariance,

                            RequiresSupervisorReview =
                                requiresSupervisorReview,

                            Unit =
                                request.Unit.Trim(),

                            Notes =
                                Clean(request.Notes)
                        };

                    _dbContext.DailyMaterialUsages.Add(
                        usage);

                    if (request.QuantityUsed > 0m)
                    {
                        _dbContext.StockMovements.Add(
                            new StockMovement
                            {
                                CompanyId =
                                    site.CompanyId,

                                StockItemId =
                                    stockItem.Id,

                                MovementType =
                                    Domain.Enums.StockMovementType.Issue,

                                MovementDateUtc =
                                    DateTime.UtcNow,

                                Quantity =
                                    request.QuantityUsed,

                                UnitCost =
                                    stockItem.AverageUnitCost,

                                ReferenceType =
                                    "DailyMaterialUsage",

                                ReferenceId =
                                    usage.Id,

                                ReferenceNumber =
                                    usage.Id.ToString(),

                                Remarks =
                                    $"Daily material usage. " +
                                    $"Used: {request.QuantityUsed} " +
                                    $"{request.Unit}."
                            });
                    }

                    if (request.QuantityReturned > 0m)
                    {
                        _dbContext.StockMovements.Add(
                            new StockMovement
                            {
                                CompanyId =
                                    site.CompanyId,

                                StockItemId =
                                    stockItem.Id,

                                MovementType =
                                    Domain.Enums.StockMovementType.Return,

                                MovementDateUtc =
                                    DateTime.UtcNow,

                                Quantity =
                                    request.QuantityReturned,

                                UnitCost =
                                    stockItem.AverageUnitCost,

                                ReferenceType =
                                    "DailyMaterialUsage",

                                ReferenceId =
                                    usage.Id,

                                ReferenceNumber =
                                    usage.Id.ToString(),

                                Remarks =
                                    $"Daily material return. " +
                                    $"Returned: {request.QuantityReturned} " +
                                    $"{request.Unit}."
                            });
                    }

                    await _dbContext.SaveChangesAsync(
                        cancellationToken);

                    usageId =
                        usage.Id;
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

        var savedUsage =
            await _dbContext.DailyMaterialUsages
                .AsNoTracking()
                .Include(x => x.ConstructionSite)
                .Include(x => x.Material)
                .Include(x => x.LoggedByUser)
                .SingleAsync(
                    x => x.Id == usageId,
                    cancellationToken);

        var quantityVariance =
            savedUsage.QuantityIssued -
            savedUsage.QuantityUsed -
            savedUsage.QuantityReturned;

        return new DailyMaterialUsageResponse
        {
            Id =
                savedUsage.Id,

            UsageNumber =
                savedUsage.UsageNumber,

            ConstructionSiteId =
                savedUsage.ConstructionSiteId,

            ConstructionSiteName =
                savedUsage.ConstructionSite.Name,

            MaterialId =
                savedUsage.MaterialId,

            MaterialCode =
                savedUsage.Material.MaterialCode,

            MaterialName =
                savedUsage.Material.Name,

            DailyProgressLogId =
                savedUsage.DailyProgressLogId,

            LoggedByUserId =
                savedUsage.LoggedByUserId,

            LoggedByUserName =
                $"{savedUsage.LoggedByUser.FirstName} " +
                $"{savedUsage.LoggedByUser.LastName}".Trim(),

            Date =
                savedUsage.Date,

            QuantityIssued =
                savedUsage.QuantityIssued,

            QuantityUsed =
                savedUsage.QuantityUsed,

            QuantityReturned =
                savedUsage.QuantityReturned,

            QuantityVariance =
                savedUsage.QuantityVariance,

            RequiresSupervisorReview =
                savedUsage.RequiresSupervisorReview,
            Unit =
                savedUsage.Unit,

            Notes =
                savedUsage.Notes
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
            ?? throw new DailyMaterialUsageManagementException(
                "Current user was not found.");
    }

    private async Task<string> GetNextUsageNumberAsync(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var lastUsageNumber =
            await _dbContext.DailyMaterialUsages
                .Where(x =>
                    x.CompanyId == companyId)
                .OrderByDescending(x => x.UsageNumber)
                .Select(x => x.UsageNumber)
                .FirstOrDefaultAsync(
                    cancellationToken);

        var nextNumber = 1;

        if (!string.IsNullOrWhiteSpace(lastUsageNumber) &&
            lastUsageNumber.StartsWith(
                "DMU-",
                StringComparison.OrdinalIgnoreCase))
        {
            var numericPart =
                lastUsageNumber.Substring(4);

            if (int.TryParse(
                    numericPart,
                    out var currentNumber))
            {
                nextNumber = currentNumber + 1;
            }
        }

        return $"DMU-{nextNumber:D6}";
    }

    private static DailyMaterialUsageResponse Map(
        DailyMaterialUsage usage)
    {
        return new DailyMaterialUsageResponse
        {
            Id = usage.Id,

            ConstructionSiteId = usage.ConstructionSiteId,
            ConstructionSiteName = usage.ConstructionSite.Name,

            MaterialId = usage.MaterialId,
            MaterialCode = usage.Material.MaterialCode,
            MaterialName = usage.Material.Name,

            DailyProgressLogId = usage.DailyProgressLogId,

            LoggedByUserId = usage.LoggedByUserId,
            LoggedByUserName =
                $"{usage.LoggedByUser.FirstName} {usage.LoggedByUser.LastName}".Trim(),

            Date = usage.Date,

            QuantityIssued = usage.QuantityIssued,
            QuantityUsed = usage.QuantityUsed,
            QuantityReturned = usage.QuantityReturned,
            QuantityVariance = usage.QuantityVariance,

            RequiresSupervisorReview = usage.RequiresSupervisorReview,

            Unit = usage.Unit,
            Notes = usage.Notes
        };
    }

    private static void ValidateRequest(
        CreateDailyMaterialUsageRequest request)
    {
        if (request.ConstructionSiteId ==
            Guid.Empty)
        {
            throw new DailyMaterialUsageManagementException(
                "Construction site is required.");
        }

        if (request.MaterialId ==
            Guid.Empty)
        {
            throw new DailyMaterialUsageManagementException(
                "Material is required.");
        }

        if (request.DailyProgressLogId ==
            Guid.Empty)
        {
            throw new DailyMaterialUsageManagementException(
                "Daily progress log is required.");
        }

        if (request.Date == default)
        {
            throw new DailyMaterialUsageManagementException(
                "Usage date is required.");
        }

        if (request.QuantityIssued <= 0m)
        {
            throw new DailyMaterialUsageManagementException(
                "Issued quantity must be greater than zero.");
        }

        if (request.QuantityUsed < 0m)
        {
            throw new DailyMaterialUsageManagementException(
                "Used quantity cannot be negative.");
        }

        if (request.QuantityReturned < 0m)
        {
            throw new DailyMaterialUsageManagementException(
                "Returned quantity cannot be negative.");
        }

        if (request.QuantityUsed >
            request.QuantityIssued)
        {
            throw new DailyMaterialUsageManagementException(
                "Used quantity cannot exceed issued quantity.");
        }

        var maximumReturnable =
            request.QuantityIssued -
            request.QuantityUsed;

        if (request.QuantityReturned >
            maximumReturnable)
        {
            throw new DailyMaterialUsageManagementException(
                "Returned quantity cannot exceed issued quantity minus used quantity.");
        }

        if (string.IsNullOrWhiteSpace(
                request.Unit))
        {
            throw new DailyMaterialUsageManagementException(
                "Unit is required.");
        }
    }

    private static void EnsureCanManage(
        User actor)
    {
        if (IsSuperAdmin(actor) ||
            HasRole(actor, AppRoles.CompanyAdmin) ||
            HasRole(actor, AppRoles.SiteManager) ||
            HasRole(actor, AppRoles.Supervisor))
        {
            return;
        }

        throw new DailyMaterialUsageManagementException(
            "You are not authorized to manage daily material usage.");
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
            throw new DailyMaterialUsageManagementException(
                "You are not authorized to access this company stock.");
        }
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
}








