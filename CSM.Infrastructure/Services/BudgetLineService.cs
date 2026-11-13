using CSM.Infrastructure.Security;
using CSM.Application.Common.Exceptions;
using CSM.Application.Finance.BudgetLines;
using CSM.Application.Finance.BudgetLines.Dtos;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Finance;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public sealed class BudgetLineService : IBudgetLineService
{
    private readonly ApplicationDbContext _dbContext;

    public BudgetLineService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<BudgetCostCodeOption>> GetCostCodeOptionsAsync(
        Guid currentUserId,
        Guid budgetId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(currentUserId, cancellationToken, GranularPermission.Finance_Budget_Manage);
        if (!actor.IsActive || actor.IsDeleted)
            throw new BudgetLineManagementException("Authenticated user is inactive.");
        EnsureCanModify(actor);

        var budget = await GetBudgetAsync(budgetId, cancellationToken);
        EnsureCompanyAccess(actor, budget.CompanyId);
        await EnsureParentScopeAsync(actor, budget, cancellationToken);

        // Match the company boundary enforced when a budget line is saved.
        return await _dbContext.CostCodes.AsNoTracking()
            .Where(x => x.CompanyId == budget.CompanyId && x.IsActive && !x.IsDeleted &&
                (!FinanceOfficerScope.IsOfficer(actor) || x.ConstructionSiteId == null || x.ConstructionSiteId == budget.ConstructionSiteId))
            .OrderBy(x => x.Code)
            .Select(x => new BudgetCostCodeOption(x.Id, x.Code, x.Name))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<BudgetLineResponse>> GetByBudgetAsync(
        Guid currentUserId,
        Guid budgetId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(currentUserId, cancellationToken, GranularPermission.Finance_Budget_View);

        EnsureCanRead(actor);

        var budget = await GetBudgetAsync(
            budgetId,
            cancellationToken);

        EnsureCompanyAccess(actor, budget.CompanyId);
        await EnsureParentScopeAsync(actor, budget, cancellationToken);

        var lines = await _dbContext.BudgetLines
            .AsNoTracking()
            .Include(x => x.CostCode)
            .Where(x => x.BudgetId == budgetId)
            .OrderBy(x => x.CostCode.Code)
            .ToListAsync(cancellationToken);

        return lines
            .Select(Map)
            .ToArray();
    }

    public async Task<BudgetLineResponse> GetByIdAsync(
        Guid currentUserId,
        Guid budgetLineId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(currentUserId, cancellationToken, GranularPermission.Finance_Budget_View);

        EnsureCanRead(actor);

        var line = await GetBudgetLineAsync(
            budgetLineId,
            cancellationToken);

        EnsureCompanyAccess(actor, line.CompanyId);
        await EnsureParentScopeAsync(actor, await GetBudgetAsync(line.BudgetId, cancellationToken), cancellationToken);

        return Map(line);
    }

    public async Task<BudgetLineResponse> CreateAsync(
        Guid currentUserId,
        CreateBudgetLineRequest request,
        CancellationToken cancellationToken = default)
    {
        return await FinanceMutationTransaction.ExecuteAsync(_dbContext, new(BudgetId: request.BudgetId), async () =>
        {
            var actor = await GetActorAsync(currentUserId, cancellationToken, GranularPermission.Finance_Budget_Manage);

            EnsureCanModify(actor);

            ValidateAmounts(
                request.BudgetedAmount,
                request.RevisedAmount);

            var budget = await GetBudgetAsync(
                request.BudgetId,
                cancellationToken);

            EnsureCompanyAccess(
                actor,
                budget.CompanyId);
            await EnsureParentScopeAsync(actor, budget, cancellationToken);

            var companyId = budget.CompanyId;

            EnsureBudgetEditable(budget);

            var costCode = await GetCostCodeAsync(
                request.CostCodeId,
                companyId,
                cancellationToken);
            await SiteFinanceScope.EnsureOfficerReferencesAsync(_dbContext, actor, budget.ConstructionSiteId, null, costCode.Id, cancellationToken, message => new BudgetLineManagementException(message));

            var duplicateExists = await _dbContext.BudgetLines
                .AnyAsync(
                    x =>
                        x.BudgetId == request.BudgetId &&
                        x.CostCodeId == request.CostCodeId &&
                        !x.IsDeleted,
                    cancellationToken);

            if (duplicateExists)
            {
                throw new BudgetLineManagementException(
                    "This cost code already exists on the selected budget.");
            }

            var line = new BudgetLine
            {
                CompanyId = companyId,
                BudgetId = budget.Id,
                CostCodeId = costCode.Id,
                Description = Clean(request.Description),
                BudgetedAmount = request.BudgetedAmount,
                RevisedAmount = request.RevisedAmount
            };

            _dbContext.BudgetLines.Add(line);

            budget.UpdatedBy = currentUserId;

            await _dbContext.SaveChangesAsync(
                cancellationToken);

            await _dbContext.Entry(line)
                .Reference(x => x.CostCode)
                .LoadAsync(cancellationToken);

            return Map(line);

        }, cancellationToken);
    }

    public async Task<BudgetLineResponse> UpdateAsync(
        Guid currentUserId,
        Guid budgetLineId,
        UpdateBudgetLineRequest request,
        CancellationToken cancellationToken = default)
    {
        var parentId = await _dbContext.BudgetLines.AsNoTracking().Where(x => x.Id == budgetLineId && !x.IsDeleted)
            .Select(x => x.BudgetId).SingleOrDefaultAsync(cancellationToken);
        return await FinanceMutationTransaction.ExecuteAsync(_dbContext, new(BudgetId: parentId), async () =>
        {
            var actor = await GetActorAsync(currentUserId, cancellationToken, GranularPermission.Finance_Budget_Manage);

            EnsureCanModify(actor);

            var line = await GetBudgetLineAsync(
                budgetLineId,
                cancellationToken);

            EnsureCompanyAccess(
                actor,
                line.CompanyId);
            await EnsureParentScopeAsync(actor, await GetBudgetAsync(line.BudgetId, cancellationToken), cancellationToken);

            var budget = await GetBudgetAsync(
                line.BudgetId,
                cancellationToken);

            EnsureCompanyAccess(
                actor,
                budget.CompanyId);
            await EnsureParentScopeAsync(actor, budget, cancellationToken);

            EnsureBudgetEditable(budget);

            ValidateAmounts(
                request.BudgetedAmount,
                request.RevisedAmount);

            var costCode = await GetCostCodeAsync(
                request.CostCodeId,
                line.CompanyId,
                cancellationToken);
            await SiteFinanceScope.EnsureOfficerReferencesAsync(_dbContext, actor, budget.ConstructionSiteId, null, costCode.Id, cancellationToken, message => new BudgetLineManagementException(message));

            var duplicateExists = await _dbContext.BudgetLines
                .AnyAsync(
                    x =>
                        x.Id != line.Id &&
                        x.BudgetId == line.BudgetId &&
                        x.CostCodeId == costCode.Id &&
                        !x.IsDeleted,
                    cancellationToken);

            if (duplicateExists)
            {
                throw new BudgetLineManagementException(
                    "This cost code already exists on the selected budget.");
            }

            var materialChange = line.CostCodeId != costCode.Id || line.Description != Clean(request.Description) ||
                line.BudgetedAmount != request.BudgetedAmount || line.RevisedAmount != request.RevisedAmount;

            line.CostCodeId = costCode.Id;
            line.Description = Clean(request.Description);
            line.BudgetedAmount = request.BudgetedAmount;
            line.RevisedAmount = request.RevisedAmount;

            if (materialChange) budget.UpdatedBy = currentUserId;

            await _dbContext.SaveChangesAsync(
                cancellationToken);

            await _dbContext.Entry(line)
                .Reference(x => x.CostCode)
                .LoadAsync(cancellationToken);

            return Map(line);

        }, cancellationToken);
    }

    private async Task EnsureParentScopeAsync(User actor, Budget parent, CancellationToken cancellationToken)
    {
        EnsureCompanyAccess(actor, parent.CompanyId);
        if (SiteFinanceScope.IsSiteLimited(actor) && !await SiteFinanceScope.HasSiteAsync(_dbContext, actor, parent.ConstructionSiteId, cancellationToken))
            throw new BudgetLineManagementException("Parent site is unavailable or outside your assigned scope.");
    }

    private async Task<User> GetActorAsync(
        Guid currentUserId,
        CancellationToken cancellationToken,
        GranularPermission action = GranularPermission.Finance_Budget_View)
    {
        var scopedActor = await FinanceOfficerScope.ResolveActorAsync(_dbContext, currentUserId, action,
            cancellationToken, message => new BudgetLineManagementException(message), supportedIndependentRoles: ["SuperAdmin", "CompanyAdmin", "Accountant"]);
        if (scopedActor is not null) return scopedActor;
        return await _dbContext.Users
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(
                x => x.Id == currentUserId,
                cancellationToken)
            ?? throw new BudgetLineManagementException(
                "Authenticated user was not found.");
    }

    private static void EnsureCanRead(User actor)
    {
        if (FinanceOfficerScope.IsOfficer(actor) || IsSuperAdmin(actor) ||
            HasRole(actor, AppRoles.CompanyAdmin) ||
            HasRole(actor, AppRoles.Accountant))
        {
            return;
        }

        throw new BudgetLineManagementException(
            "You are not authorized to view budget lines.");
    }

    private static void EnsureCanModify(User actor)
    {
        if (FinanceOfficerScope.IsOfficer(actor) || IsSuperAdmin(actor) ||
            HasRole(actor, AppRoles.CompanyAdmin) ||
            HasRole(actor, AppRoles.Accountant))
        {
            return;
        }

        throw new BudgetLineManagementException(
            "You are not authorized to manage budget lines.");
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
            throw new BudgetLineManagementException(
                "You are not authorized to access this budget line.");
        }
    }

    private async Task<Budget> GetBudgetAsync(
        Guid budgetId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Budgets
            .SingleOrDefaultAsync(
                x =>
                    x.Id == budgetId &&
                    !x.IsDeleted,
                cancellationToken)
            ?? throw new BudgetLineManagementException(
                "Budget was not found.");
    }

    private async Task<BudgetLine> GetBudgetLineAsync(
        Guid budgetLineId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.BudgetLines
            .Include(x => x.CostCode)
            .SingleOrDefaultAsync(
                x =>
                    x.Id == budgetLineId &&
                    !x.IsDeleted,
                cancellationToken)
            ?? throw new BudgetLineManagementException(
                "Budget line was not found.");
    }

    private async Task<CostCode> GetCostCodeAsync(
        Guid costCodeId,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.CostCodes
            .SingleOrDefaultAsync(
                x =>
                    x.Id == costCodeId &&
                    x.CompanyId == companyId &&
                    !x.IsDeleted,
                cancellationToken)
            ?? throw new BudgetLineManagementException(
                "Cost code was not found or does not belong to the current company.");
    }

    public async Task DeleteAsync(
        Guid currentUserId,
        Guid budgetLineId,
        CancellationToken cancellationToken = default)
    {
        var parentId = await _dbContext.BudgetLines.AsNoTracking().Where(x => x.Id == budgetLineId && !x.IsDeleted)
            .Select(x => x.BudgetId).SingleOrDefaultAsync(cancellationToken);
        await FinanceMutationTransaction.ExecuteAsync(_dbContext, new(BudgetId: parentId), async () =>
        {
            var actor = await GetActorAsync(currentUserId, cancellationToken, GranularPermission.Finance_Budget_Manage);

            EnsureCanModify(actor);

            var line = await GetBudgetLineAsync(
                budgetLineId,
                cancellationToken);

            EnsureCompanyAccess(
                actor,
                line.CompanyId);
            await EnsureParentScopeAsync(actor, await GetBudgetAsync(line.BudgetId, cancellationToken), cancellationToken);

            var budget = await GetBudgetAsync(
                line.BudgetId,
                cancellationToken);

            EnsureCompanyAccess(
                actor,
                budget.CompanyId);
            await EnsureParentScopeAsync(actor, budget, cancellationToken);

            EnsureBudgetEditable(budget);

            line.IsDeleted = true;

            budget.UpdatedBy = currentUserId;

            await _dbContext.SaveChangesAsync(
                cancellationToken);

        }, cancellationToken);
    }

    private static void EnsureBudgetEditable(
        Budget budget)
    {
        if (budget.Status is not (BudgetStatus.Draft or BudgetStatus.PendingApproval))
        {
            throw new BudgetLineManagementException(
                $"Budget lines cannot be modified when the budget is {budget.Status}.");
        }
    }

    private static void ValidateAmounts(
        decimal budgetedAmount,
        decimal revisedAmount)
    {
        if (budgetedAmount < 0)
        {
            throw new BudgetLineManagementException(
                "Budgeted amount cannot be negative.");
        }

        if (revisedAmount < 0)
        {
            throw new BudgetLineManagementException(
                "Revised amount cannot be negative.");
        }
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

    private static BudgetLineResponse Map(
        BudgetLine line)
    {
        return new BudgetLineResponse(
            line.Id,
            line.CompanyId,
            line.BudgetId,
            line.CostCodeId,
            line.CostCode.Code,
            line.Description,
            line.BudgetedAmount,
            line.RevisedAmount,
            line.CreatedAtUtc,
            line.UpdatedAtUtc);
    }
}
