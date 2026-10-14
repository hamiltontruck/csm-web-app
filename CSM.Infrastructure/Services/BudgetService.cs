using CSM.Application.Common.Exceptions;
using CSM.Application.Finance.Budgets;
using CSM.Application.Finance.Budgets.Dtos;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Finance;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using CSM.Infrastructure.Security;

namespace CSM.Infrastructure.Services;

public sealed class BudgetService : IBudgetService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IPermissionEvaluator _permissionEvaluator;

    public BudgetService(
        ApplicationDbContext dbContext,
        IPermissionEvaluator permissionEvaluator)
    {
        _dbContext = dbContext;
        _permissionEvaluator = permissionEvaluator;
    }

    public async Task<IReadOnlyCollection<BudgetResponse>> GetAllAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        await EnsureCanRead(
            actor,
            cancellationToken);

        IQueryable<Budget> query =
            _dbContext.Budgets.AsNoTracking();

        if (!IsSuperAdmin(actor))
        {
            query = query.Where(
                x => x.CompanyId == GetActorCompanyId(actor));
        }

        var budgets = await query
            .OrderBy(x => x.BudgetNumber)
            .ToListAsync(cancellationToken);

        return budgets
            .Select(Map)
            .ToArray();
    }

    public async Task<IReadOnlyCollection<BudgetResponse>> GetBySiteAsync(
        Guid currentUserId,
        Guid siteId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(currentUserId, cancellationToken);

        await EnsureCanRead(
            actor,
            cancellationToken);

        var companyId = GetActorCompanyId(actor);

        await EnsureSiteAccess(
            actor,
            siteId,
            cancellationToken);

        var budgets = await _dbContext.Budgets
            .AsNoTracking()
            .Include(x => x.ConstructionSite)
            .Include(x => x.Project)
            .Where(x =>
                x.CompanyId == companyId &&
                x.ConstructionSiteId == siteId &&
                !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return budgets.Select(Map).ToArray();
    }

    public async Task<BudgetResponse> GetByIdAsync(
        Guid currentUserId,
        Guid budgetId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        await EnsureCanRead(
            actor,
            cancellationToken);

        var budget = await GetBudgetAsync(
            budgetId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            budget.CompanyId);

        return Map(budget);
    }

    public async Task<BudgetResponse> CreateAsync(
        Guid currentUserId,
        CreateBudgetRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        await EnsureCanModify(
            actor,
            cancellationToken);

        var companyId = GetActorCompanyId(actor);

        var budgetNumber = CleanRequired(
            request.BudgetNumber,
            "Budget number");

        var name = CleanRequired(
            request.Name,
            "Budget name");

        var currencyCode = CleanRequired(
            request.CurrencyCode,
            "Currency code")
            .ToUpperInvariant();

        ValidateMoney(
            request.TotalAmount,
            "Total amount");

        ValidateDates(
            request.EffectiveFrom,
            request.EffectiveTo);

        if (currencyCode.Length != 3)
        {
            throw new BudgetManagementException(
                "Currency code must contain exactly 3 characters.");
        }

        var duplicateExists = await _dbContext.Budgets
            .AnyAsync(
                x =>
                    x.CompanyId == companyId &&
                    x.BudgetNumber == budgetNumber,
                cancellationToken);

        if (duplicateExists)
        {
            throw new BudgetManagementException(
                $"Budget number '{budgetNumber}' already exists.");
        }

        if (request.ConstructionSiteId.HasValue)
        {
            await EnsureSiteAccess(
                actor,
                request.ConstructionSiteId,
                cancellationToken);
            await EnsureProjectAccess(
                companyId,
                request.ConstructionSiteId.Value,
                request.ProjectId,
                cancellationToken);
        }
        else if (request.ProjectId.HasValue)
        {
            throw new BudgetManagementException(
                "A company-level budget cannot be assigned to a project.");
        }

        var budget = new Budget
        {
            CompanyId = companyId,
            ConstructionSiteId =
                request.ConstructionSiteId,
            ProjectId = request.ProjectId,
            BudgetNumber = budgetNumber,
            Name = name,
            Description = Clean(request.Description),
            TotalAmount = request.TotalAmount,
            CurrencyCode = currencyCode,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            Status = BudgetStatus.Draft
        };

        _dbContext.Budgets.Add(budget);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(budget);
    }

    public async Task<BudgetResponse> UpdateAsync(
        Guid currentUserId,
        Guid budgetId,
        UpdateBudgetRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        await EnsureCanModify(
            actor,
            cancellationToken);

        var budget = await GetBudgetForUpdateAsync(
            budgetId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            budget.CompanyId);

        await EnsureSiteAccess(
            actor,
            budget.ConstructionSiteId,
            cancellationToken);

        EnsureEditable(budget);

        var name = CleanRequired(
            request.Name,
            "Budget name");

        var currencyCode = CleanRequired(
            request.CurrencyCode,
            "Currency code")
            .ToUpperInvariant();

        ValidateMoney(
            request.TotalAmount,
            "Total amount");

        ValidateDates(
            request.EffectiveFrom,
            request.EffectiveTo);

        if (currencyCode.Length != 3)
        {
            throw new BudgetManagementException(
                "Currency code must contain exactly 3 characters.");
        }

        budget.Name = name;
        budget.Description = Clean(request.Description);
        budget.TotalAmount = request.TotalAmount;
        budget.CurrencyCode = currencyCode;
        budget.EffectiveFrom = request.EffectiveFrom;
        budget.EffectiveTo = request.EffectiveTo;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(budget);
    }

    public async Task<BudgetResponse> ChangeStatusAsync(
        Guid currentUserId,
        Guid budgetId,
        ChangeBudgetStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        await EnsureCanModify(
            actor,
            cancellationToken);

        var budget = await GetBudgetForUpdateAsync(
            budgetId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            budget.CompanyId);

        await EnsureSiteAccess(
            actor,
            budget.ConstructionSiteId,
            cancellationToken);

        ValidateStatusTransition(
            budget.Status,
            request.Status);

        if (request.Status == BudgetStatus.Approved)
        {
            budget.ApprovedBy = currentUserId;
            budget.ApprovedAtUtc = DateTime.UtcNow;
        }

        budget.Status = request.Status;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(budget);
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
            ?? throw new BudgetManagementException(
                "Authenticated user was not found.");
    }

    private async Task EnsureCanRead(
        User actor,
        CancellationToken cancellationToken)
    {
        var hasPermission = await _permissionEvaluator.HasPermissionAsync(
            actor.Id,
            GranularPermission.Finance_Budget_View.ToString(),
            cancellationToken);

        if (!hasPermission)
        {
            throw new BudgetManagementException(
                "You are not authorized to view budgets.");
        }
    }

    private async Task EnsureCanModify(
        User actor,
        CancellationToken cancellationToken)
    {
        var hasPermission = await _permissionEvaluator.HasPermissionAsync(
            actor.Id,
            GranularPermission.Finance_Budget_Manage.ToString(),
            cancellationToken);

        if (!hasPermission)
        {
            throw new BudgetManagementException(
                "You are not authorized to manage budgets.");
        }
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
            throw new BudgetManagementException(
                "You are not authorized to access this budget.");
        }
    }

    private static Guid GetActorCompanyId(User actor)
    {
        if (!actor.CompanyId.HasValue)
        {
            throw new BudgetManagementException(
                "Current user is not assigned to a company.");
        }

        return actor.CompanyId.Value;
    }

    private async Task<Budget> GetBudgetAsync(
        Guid budgetId,
        CancellationToken cancellationToken)
    {
        var budget = await _dbContext.Budgets
            .Include(x => x.ConstructionSite)
            .Include(x => x.Project)
            .SingleOrDefaultAsync(
                x => x.Id == budgetId && !x.IsDeleted,
                cancellationToken);

        if (budget is null)
        {
            throw new BudgetManagementException("Budget not found.");
        }

        return budget;
    }

    private async Task<Budget> GetBudgetForUpdateAsync(
        Guid budgetId,
        CancellationToken cancellationToken)
    {
        var budget = await _dbContext.Budgets
            .Include(x => x.ConstructionSite)
            .Include(x => x.Project)
            .SingleOrDefaultAsync(
                x => x.Id == budgetId && !x.IsDeleted,
                cancellationToken);

        if (budget is null)
        {
            throw new BudgetManagementException(
                "Budget not found.");
        }

        return budget;
    }

    private async Task EnsureSiteAccess(
        User actor,
        Guid? siteId,
        CancellationToken cancellationToken)
    {
        if (!siteId.HasValue)
        {
            return;
        }

        if (IsSuperAdmin(actor))
        {
            return;
        }

        var companyId = GetActorCompanyId(actor);

        var siteExists = await _dbContext.ConstructionSites
            .AnyAsync(
                x =>
                    x.Id == siteId.Value &&
                    x.CompanyId == companyId &&
                    !x.IsDeleted,
                cancellationToken);

        if (!siteExists)
        {
            throw new BudgetManagementException(
                "The selected construction site is invalid or inaccessible.");
        }

        if (HasRole(actor, AppRoles.CompanyAdmin) ||
            HasRole(actor, AppRoles.Accountant))
        {
            return;
        }

        var hasSiteAccess = await _dbContext.UserSiteAccesses
            .AnyAsync(
                x =>
                    x.UserId == actor.Id &&
                    x.SiteId == siteId.Value &&
                    x.CompanyId == companyId &&
                    x.IsActive,
                cancellationToken);

        if (!hasSiteAccess)
        {
            throw new BudgetManagementException(
                "You are not assigned to this site's budget scope.");
        }
    }

    private async Task EnsureProjectAccess(
        Guid companyId,
        Guid siteId,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        if (!projectId.HasValue)
        {
            return;
        }

        var exists = await _dbContext.Projects
            .AnyAsync(
                x =>
                    x.Id == projectId.Value &&
                    x.CompanyId == companyId &&
                    x.ConstructionSiteId == siteId &&
                    !x.IsDeleted,
                cancellationToken);

        if (!exists)
        {
            throw new BudgetManagementException(
                "The selected project is invalid, inaccessible, or belongs to another construction site.");
        }
    }

    private static void EnsureEditable(Budget budget)
    {
        if (budget.Status != BudgetStatus.Draft &&
            budget.Status != BudgetStatus.PendingApproval)
        {
            throw new BudgetManagementException(
                "Only Draft or PendingApproval budgets can be edited.");
        }
    }

    private static void ValidateStatusTransition(
        BudgetStatus current,
        BudgetStatus next)
    {
        if (current == next)
        {
            throw new BudgetManagementException(
                $"Budget is already in {next} status.");
        }

        var valid = current switch
        {
            BudgetStatus.Draft =>
                next == BudgetStatus.PendingApproval ||
                next == BudgetStatus.Cancelled,

            BudgetStatus.PendingApproval =>
                next == BudgetStatus.Approved ||
                next == BudgetStatus.Draft ||
                next == BudgetStatus.Cancelled,

            BudgetStatus.Approved =>
                next == BudgetStatus.Active ||
                next == BudgetStatus.Cancelled,

            BudgetStatus.Active =>
                next == BudgetStatus.Closed,

            BudgetStatus.Closed =>
                false,

            BudgetStatus.Cancelled =>
                false,

            _ => false
        };

        if (!valid)
        {
            throw new BudgetManagementException(
                $"Invalid budget status transition from {current} to {next}.");
        }
    }

    private static void ValidateMoney(
        decimal amount,
        string fieldName)
    {
        if (amount < 0)
        {
            throw new BudgetManagementException(
                $"{fieldName} cannot be negative.");
        }
    }

    private static void ValidateDates(
        DateOnly? from,
        DateOnly? to)
    {
        if (from.HasValue &&
            to.HasValue &&
            to.Value < from.Value)
        {
            throw new BudgetManagementException(
                "EffectiveTo cannot be earlier than EffectiveFrom.");
        }
    }

    private static string CleanRequired(
        string? value,
        string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BudgetManagementException(
                $"{fieldName} is required.");
        }

        return value.Trim();
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static bool IsSuperAdmin(User actor)
    {
        return HasRole(actor, AppRoles.SuperAdmin);
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

    private static BudgetResponse Map(Budget budget)
    {
        return new BudgetResponse
        {
            Id = budget.Id,
            CompanyId = budget.CompanyId,

            ConstructionSiteId = budget.ConstructionSiteId,
            ConstructionSiteName = budget.ConstructionSite?.Name ?? string.Empty,

            ProjectId = budget.ProjectId,
            ProjectName = budget.Project == null
                ? null
                : $"{budget.Project.ProjectCode} - {budget.Project.Name}",

            BudgetNumber = budget.BudgetNumber,
            Name = budget.Name,
            Description = budget.Description,

            TotalAmount = budget.TotalAmount,
            CurrencyCode = budget.CurrencyCode,

            EffectiveFrom = budget.EffectiveFrom!.Value,
            EffectiveTo = budget.EffectiveTo ?? budget.EffectiveFrom,

            Status = budget.Status,

            ApprovedBy = budget.ApprovedBy,
            ApprovedAtUtc = budget.ApprovedAtUtc,

            CreatedAtUtc = budget.CreatedAtUtc,
            UpdatedAtUtc = budget.UpdatedAtUtc
        };
    }
}

