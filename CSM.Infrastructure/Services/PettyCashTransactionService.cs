using Npgsql;
using CSM.Domain.Enums;
using CSM.Infrastructure.Security;
using CSM.Application.Common.Exceptions;
using CSM.Application.Finance.PettyCashTransactions;
using CSM.Application.Finance.PettyCashTransactions.Dtos;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Finance;
using CSM.Domain.Entities.Identity;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public sealed class PettyCashTransactionService
    : IPettyCashTransactionService
{
    private readonly ApplicationDbContext _dbContext;

    public PettyCashTransactionService(
        ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<PettyCashCostCodeOption>> GetCostCodeOptionsAsync(
        Guid currentUserId, Guid pettyCashAccountId, CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(currentUserId, cancellationToken, GranularPermission.Finance_PettyCash_Manage);
        var permissions = new PermissionEvaluator(_dbContext);
        if (!await permissions.HasPermissionAsync(currentUserId, "Finance_PettyCash_View", cancellationToken) ||
            !await permissions.HasPermissionAsync(currentUserId, "Finance_PettyCash_Manage", cancellationToken))
            throw new PettyCashManagementException("Petty cash transaction access is not permitted.");

        var account = await GetAccountAsync(pettyCashAccountId, cancellationToken);
        await EnsureAccountAccessAsync(actor, account, cancellationToken);
        var codes = _dbContext.CostCodes.AsNoTracking()
            .Where(x => x.CompanyId == account.CompanyId && x.IsActive && !x.IsDeleted);
        if (SiteFinanceScope.IsSiteLimited(actor))
            codes = codes.Where(x => x.ConstructionSiteId == null || x.ConstructionSiteId == account.ConstructionSiteId);
        return await codes.OrderBy(x => x.Code)
            .Select(x => new PettyCashCostCodeOption(x.Id, x.Code, x.Name))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<PettyCashTransactionResponse>> GetAllAsync(
        Guid currentUserId,
        Guid pettyCashAccountId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(currentUserId, cancellationToken, GranularPermission.Finance_PettyCash_View);

        var account = await GetAccountAsync(
            pettyCashAccountId,
            cancellationToken);

        await EnsureAccountAccessAsync(actor, account, cancellationToken);

        var records = await _dbContext.PettyCashTransactions
            .AsNoTracking()
            .Include(x => x.CostCode)
            .Where(x => x.PettyCashAccountId == pettyCashAccountId)
            .OrderByDescending(x => x.TransactionDateUtc)
            .ToListAsync(cancellationToken);

        return records
            .Select(Map)
            .ToList();
    }

    public async Task<PettyCashTransactionResponse> CreateAsync(
        Guid currentUserId,
        CreatePettyCashTransactionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await FinanceMutationTransaction.ExecuteAsync(_dbContext,
                new(ExpenseIds: request.ExpenseId is Guid id ? [id] : [], AccountId: request.PettyCashAccountId), async () =>
            {
                var actor = await GetActorAsync(currentUserId, cancellationToken, GranularPermission.Finance_PettyCash_Manage);

                var account = await GetAccountAsync(
                    request.PettyCashAccountId,
                    cancellationToken);

                await EnsureAccountAccessAsync(actor, account, cancellationToken);

                if (!account.IsActive) throw new PettyCashManagementException("An active petty cash account is required.");
                var type = request.TransactionType?.Trim();
                type = string.Equals(type, "CashIn", StringComparison.OrdinalIgnoreCase) ? "CashIn"
                    : string.Equals(type, "CashOut", StringComparison.OrdinalIgnoreCase) ? "CashOut"
                    : throw new PettyCashManagementException("Transaction type must be CashIn or CashOut.");
                if (request.Amount <= 0 || request.Amount >= 1000000000000000m || decimal.Round(request.Amount, 4) != request.Amount)
                    throw new PettyCashManagementException("Amount must be positive with at most four decimal places.");
                Expense? expense = null;
                var settlement = new ExpenseSettlementService(_dbContext);
                if (request.ExpenseId is Guid expenseId)
                {
                    expense = await settlement.LoadForDisbursementAsync(currentUserId, expenseId, "petty-cash", cancellationToken);
                    if (type != "CashOut" || account.CompanyId != expense.CompanyId || account.ConstructionSiteId != expense.ConstructionSiteId || request.CostCodeId != expense.CostCodeId)
                        throw new PettyCashManagementException("Expense Cash Out must match its company, site and cost code.");
                    var reference = request.ReferenceNumber?.Trim();
                    if (string.IsNullOrWhiteSpace(reference) || reference.Length > 100)
                        throw new PettyCashManagementException("A reference number of at most 100 characters is required for expense Cash Out.");
                    if (await _dbContext.PettyCashTransactions.IgnoreQueryFilters().AnyAsync(x => x.PettyCashAccountId == account.Id && x.ExpenseId != null && x.ReferenceNumber == reference, cancellationToken))
                        throw new PettyCashManagementException("This expense Cash Out reference has already been posted for this account.");
                    await settlement.ValidateNewSettlementAsync(expense, request.Amount, "ETB", 1m, true, cancellationToken);
                }

                var siteLimited = SiteFinanceScope.IsSiteLimited(actor);
                if (request.CostCodeId is Guid costCodeId &&
                    !await _dbContext.CostCodes.AnyAsync(x =>
                        x.Id == costCodeId && x.CompanyId == account.CompanyId && !x.IsDeleted &&
                        (!siteLimited || x.ConstructionSiteId == null || x.ConstructionSiteId == account.ConstructionSiteId),
                        cancellationToken))
                {
                    throw new PettyCashManagementException("Cost code does not belong to this petty cash account's permitted scope.");
                }

                if (request.Amount <= 0)
                {
                    throw new PettyCashManagementException(
                        "Amount must be greater than zero.");
                }

                var before = account.CurrentBalance;

                decimal after = type.Equals(
                    "CashIn",
                    StringComparison.OrdinalIgnoreCase)
                    ? before + request.Amount
                    : before - request.Amount;

                if (after < 0)
                {
                    throw new PettyCashManagementException(
                        "Insufficient petty cash balance.");
                }

                var transaction = new PettyCashTransaction
                {
                    ExpenseId = expense?.Id,
                    CreatedBy = currentUserId,
                    UpdatedBy = currentUserId,
                    PettyCashAccountId = account.Id,
                    CostCodeId = request.CostCodeId,
                    CreatedByUserId = currentUserId,
                    ReferenceNumber = request.ReferenceNumber?.Trim() ?? string.Empty,
                    TransactionType = type,
                    TransactionDateUtc = DateTime.UtcNow,
                    Amount = request.Amount,
                    BalanceBefore = before,
                    BalanceAfter = after,
                    Description = request.Description?.Trim() ?? string.Empty,
                    Remarks = string.IsNullOrWhiteSpace(request.Remarks)
                        ? null
                        : request.Remarks.Trim(),
                    IsApproved = true,
                    ApprovedAtUtc = DateTime.UtcNow,
                    ApprovedByUserId = currentUserId
                };

                account.CurrentBalance = after;
                account.UpdatedBy = currentUserId;

                _dbContext.PettyCashTransactions.Add(transaction);

                await _dbContext.SaveChangesAsync(cancellationToken);

                if (expense is not null)
                {
                    await settlement.SynchronizeAsync(expense, cancellationToken);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }

                if (transaction.CostCodeId.HasValue)
                {
                    transaction.CostCode = await _dbContext.CostCodes
                        .FindAsync(
                            [transaction.CostCodeId.Value],
                            cancellationToken);
                }

                return Map(transaction);
            }, cancellationToken);
        }
        catch (ExpenseManagementException ex) { throw new PettyCashManagementException(ex.Message); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { throw new PettyCashManagementException("This expense Cash Out reference has already been posted for this account."); }
    }

    private async Task EnsureAccountAccessAsync(
        User actor, PettyCashAccount account, CancellationToken cancellationToken)
    {
        EnsureCompanyAccess(actor, account.CompanyId);

        if (FinanceOfficerScope.IsOfficer(actor) && !await SiteFinanceScope.HasSiteAsync(_dbContext, actor, account.ConstructionSiteId, cancellationToken))
            throw new PettyCashManagementException("Petty cash site is unavailable or outside your assigned scope.");
        if (SiteFinanceScope.IsSiteLimited(actor))
        {
            if (!account.ConstructionSiteId.HasValue)
            {
                throw new PettyCashManagementException(
                    "You are not authorized to access this petty cash account.");
            }

            var hasSiteAccess = await _dbContext.UserSiteAccesses
                .AnyAsync(
                    x =>
                        x.UserId == actor.Id &&
                        x.SiteId == account.ConstructionSiteId.Value &&
                        x.CompanyId == actor.CompanyId &&
                        x.IsActive && !x.IsDeleted &&
                        _dbContext.ConstructionSites.Any(s => s.Id == x.SiteId && s.CompanyId == x.CompanyId && !s.IsDeleted),
                    cancellationToken);

            if (!hasSiteAccess)
            {
                throw new PettyCashManagementException(
                    "You are not assigned to this site's petty cash scope.");
            }
        }
    }

    private async Task<User> GetActorAsync(
        Guid currentUserId,
        CancellationToken cancellationToken,
        GranularPermission action = GranularPermission.Finance_PettyCash_View)
    {
        var scopedActor = await FinanceOfficerScope.ResolveActorAsync(_dbContext, currentUserId, action,
            cancellationToken, message => new PettyCashManagementException(message), supportedIndependentRoles: action == GranularPermission.Finance_PettyCash_Manage ? ["SuperAdmin", "CompanyAdmin", "Accountant"] : ["SuperAdmin", "CompanyAdmin", "Accountant", "SiteManager"]);
        if (scopedActor is not null) return scopedActor;
        return await _dbContext.Users
            .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(x => x.Id == currentUserId && x.IsActive && !x.IsDeleted, cancellationToken)
            ?? throw new PettyCashManagementException("Authenticated user was not found.");
    }

    private async Task<PettyCashAccount> GetAccountAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await _dbContext.PettyCashAccounts
            .SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken)
            ?? throw new PettyCashManagementException("Petty cash account was not found.");
    }

    private static void EnsureCompanyAccess(
        User actor,
        Guid companyId)
    {
        if (HasRole(actor, AppRoles.SuperAdmin))
            return;

        if (actor.CompanyId != companyId)
        {
            throw new PettyCashManagementException(
                "You are not authorized to access this petty cash account.");
        }
    }

    private static bool HasRole(User user, string role)
    {
        return user.UserRoles.Any(x =>
            !x.IsDeleted &&
            !x.Role.IsDeleted &&
            string.Equals(
                x.Role.Name,
                role,
                StringComparison.OrdinalIgnoreCase));
    }

    private static PettyCashTransactionResponse Map(
        PettyCashTransaction tx)
    {
        return new PettyCashTransactionResponse
        {
            Id = tx.Id,
            ExpenseId = tx.ExpenseId,
            PettyCashAccountId = tx.PettyCashAccountId,
            CostCodeId = tx.CostCodeId,
            CostCodeName = tx.CostCode?.Name,
            ReferenceNumber = tx.ReferenceNumber,
            TransactionType = tx.TransactionType,
            TransactionDateUtc = tx.TransactionDateUtc,
            Amount = tx.Amount,
            BalanceBefore = tx.BalanceBefore,
            BalanceAfter = tx.BalanceAfter,
            Description = tx.Description,
            Remarks = tx.Remarks,
            IsApproved = tx.IsApproved
        };
    }
}
