using CSM.Infrastructure.Security;
using CSM.Application.Common.Exceptions;
using CSM.Application.Finance.Expenses;
using CSM.Application.Finance.Expenses.Dtos;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Finance;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public sealed class ExpenseService : IExpenseService
{
    private readonly ApplicationDbContext _dbContext;

    public ExpenseService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<ExpenseResponse>> GetAllAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(currentUserId, cancellationToken, GranularPermission.Finance_Expense_View);

        EnsureCanRead(actor);

        await EnsureSiteAccess(actor, null, cancellationToken);

        IQueryable<Expense> query =
            _dbContext.Expenses.AsNoTracking();

        if (!IsSuperAdmin(actor))
        {
            var companyId = GetActorCompanyId(actor);

            query = query.Where(
                x => x.CompanyId == companyId);
        }

        query = query.Where(
            x => x.ConstructionSiteId == null);

        var expenses = await query
            .OrderByDescending(x => x.ExpenseDate)
            .ThenBy(x => x.ExpenseNumber)
            .ToListAsync(cancellationToken);

        return expenses
            .Select(x => Map(x))
            .ToArray();
    }

    public async Task<IReadOnlyCollection<ExpenseResponse>> GetBySiteAsync(
        Guid currentUserId,
        Guid constructionSiteId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(currentUserId, cancellationToken, GranularPermission.Finance_Expense_View);

        EnsureCanRead(actor);

        await EnsureSiteAccess(
            actor,
            constructionSiteId,
            cancellationToken);

        var companyId = GetActorCompanyId(actor);

        var siteExists = await _dbContext.ConstructionSites
            .AnyAsync(
                x =>
                    x.Id == constructionSiteId &&
                    x.CompanyId == companyId &&
                    !x.IsDeleted,
                cancellationToken);

        if (!siteExists)
        {
            throw new ExpenseManagementException(
                "Construction site was not found or does not belong to the current company.");
        }

        var expenses = await _dbContext.Expenses
            .AsNoTracking()
            .Where(
                x =>
                    x.CompanyId == companyId &&
                    x.ConstructionSiteId == constructionSiteId &&
                    !x.IsDeleted)
            .OrderByDescending(x => x.ExpenseDate)
            .ThenBy(x => x.ExpenseNumber)
            .ToListAsync(cancellationToken);

        return expenses
            .Select(x => Map(x))
            .ToArray();
    }

    public async Task<ExpenseResponse> GetByIdAsync(
        Guid currentUserId,
        Guid expenseId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(currentUserId, cancellationToken, GranularPermission.Finance_Expense_View);

        EnsureCanRead(actor);

        var expense = await GetExpenseAsync(
            expenseId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            expense.CompanyId);

        await EnsureSiteAccess(
            actor,
            expense.ConstructionSiteId,
            cancellationToken);

        var approvedByName = expense.ApprovedBy.HasValue
            ? await _dbContext.Users
                .Where(x => x.Id == expense.ApprovedBy.Value)
                .Select(x => x.FirstName + " " + x.LastName)
                .SingleOrDefaultAsync(cancellationToken)
            : null;

        var approvalReason = await FinanceApprovalPolicy.GetBlockReasonAsync(_dbContext, currentUserId,
            expense, expense.CompanyId, expense.ConstructionSiteId, GranularPermission.Finance_Expense_Approve,
            expense.Status == ExpenseStatus.Submitted, cancellationToken);
        return Map(expense, approvedByName) with { CanApprove = approvalReason is null, ApprovalBlockReason = approvalReason,
            Settlement = await new ExpenseSettlementService(_dbContext).GetSummaryAsync(expense, cancellationToken) };
    }

    public async Task<ExpenseResponse> CreateAsync(
        Guid currentUserId,
        CreateExpenseRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(currentUserId, cancellationToken, GranularPermission.Finance_Expense_Submit);

        EnsureCanSubmit(actor);

        var companyId = await ResolveCreateCompanyAsync(actor, request.CostCodeId, cancellationToken);

        await EnsureSiteAccess(
            actor,
            request.ConstructionSiteId,
            cancellationToken);

        var expenseNumber = CleanRequired(
            request.ExpenseNumber,
            "Expense number");

        var description = CleanRequired(
            request.Description,
            "Expense description");

        ValidateAmounts(
            request.Amount,
            request.TaxAmount,
            request.ExchangeRate);

        await SiteFinanceScope.EnsureOfficerReferencesAsync(_dbContext, actor, request.ConstructionSiteId, request.ProjectId, request.CostCodeId, cancellationToken, message => new ExpenseManagementException(message));
        await EnsureReferencesBelongToCompanyAsync(
            companyId,
            request.ConstructionSiteId,
            request.ProjectId,
            request.CostCodeId,
            request.VendorId,
            cancellationToken);

        var duplicateExists = await _dbContext.Expenses
            .AnyAsync(
                x =>
                    x.CompanyId == companyId &&
                    x.ExpenseNumber == expenseNumber &&
                    !x.IsDeleted,
                cancellationToken);

        if (duplicateExists)
        {
            throw new ExpenseManagementException(
                $"Expense number '{expenseNumber}' already exists.");
        }

        var expense = new Expense
        {
            CreatedBy = currentUserId,
            UpdatedBy = currentUserId,
            CompanyId = companyId,
            ConstructionSiteId =
                request.ConstructionSiteId,
            ProjectId = request.ProjectId,
            CostCodeId = request.CostCodeId,
            VendorId = request.VendorId,
            ExpenseNumber = expenseNumber,
            ExpenseDate = request.ExpenseDate,
            Description = description,
            Amount = request.Amount,
            TaxAmount = request.TaxAmount,
            CurrencyCode = CleanCurrencyCode(
                request.CurrencyCode),
            ExchangeRate = request.ExchangeRate,
            ReferenceNumber =
                Clean(request.ReferenceNumber),
            ReceiptDocumentUrl =
                Clean(request.ReceiptDocumentUrl),
            Status = ExpenseStatus.Draft
        };

        _dbContext.Expenses.Add(expense);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(expense);
    }

    public async Task<ExpenseResponse> UpdateAsync(
        Guid currentUserId,
        Guid expenseId,
        UpdateExpenseRequest request,
        CancellationToken cancellationToken = default)
    {
        return await FinanceMutationTransaction.ExecuteAsync(_dbContext, new(ExpenseIds: [expenseId]), async () =>
        {
            var actor = await GetActorAsync(currentUserId, cancellationToken, GranularPermission.Finance_Expense_Submit);

            EnsureCanSubmit(actor);

            var expense = await GetExpenseAsync(
                expenseId,
                cancellationToken);

            EnsureCompanyAccess(
                actor,
                expense.CompanyId);

            await EnsureSiteAccess(
                actor,
                expense.ConstructionSiteId,
                cancellationToken);

            await EnsureSiteAccess(
                actor,
                request.ConstructionSiteId,
                cancellationToken);

            EnsureEditable(expense);

            var description = CleanRequired(
                request.Description,
                "Expense description");

            ValidateAmounts(
                request.Amount,
                request.TaxAmount,
                request.ExchangeRate);

            await SiteFinanceScope.EnsureOfficerReferencesAsync(_dbContext, actor, request.ConstructionSiteId, request.ProjectId, request.CostCodeId, cancellationToken, message => new ExpenseManagementException(message));
            await EnsureReferencesBelongToCompanyAsync(
                expense.CompanyId,
                request.ConstructionSiteId,
                request.ProjectId,
                request.CostCodeId,
                request.VendorId,
                cancellationToken);

            var materialChange = expense.ConstructionSiteId != request.ConstructionSiteId || expense.ProjectId != request.ProjectId ||
                expense.CostCodeId != request.CostCodeId || expense.VendorId != request.VendorId || expense.ExpenseDate != request.ExpenseDate ||
                expense.Description != description || expense.Amount != request.Amount || expense.TaxAmount != request.TaxAmount ||
                expense.CurrencyCode != CleanCurrencyCode(request.CurrencyCode) || expense.ExchangeRate != request.ExchangeRate ||
                expense.ReferenceNumber != Clean(request.ReferenceNumber) || expense.ReceiptDocumentUrl != Clean(request.ReceiptDocumentUrl);

            expense.ConstructionSiteId =
                request.ConstructionSiteId;
            expense.ProjectId =
                request.ProjectId;
            expense.CostCodeId =
                request.CostCodeId;
            expense.VendorId =
                request.VendorId;
            expense.ExpenseDate =
                request.ExpenseDate;
            expense.Description =
                description;
            expense.Amount =
                request.Amount;
            expense.TaxAmount =
                request.TaxAmount;
            expense.CurrencyCode =
                CleanCurrencyCode(
                    request.CurrencyCode);
            expense.ExchangeRate =
                request.ExchangeRate;
            expense.ReferenceNumber =
                Clean(request.ReferenceNumber);
            expense.ReceiptDocumentUrl =
                Clean(request.ReceiptDocumentUrl);

            if (materialChange) expense.UpdatedBy = currentUserId;

            await _dbContext.SaveChangesAsync(
                cancellationToken);

            return Map(expense);

        }, cancellationToken);
    }

    public async Task<ExpenseResponse> ChangeStatusAsync(
        Guid currentUserId,
        Guid expenseId,
        ChangeExpenseStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        return await FinanceMutationTransaction.ExecuteAsync(_dbContext, new(ExpenseIds: [expenseId]), async () =>
        {
            var actor = await GetActorAsync(currentUserId, cancellationToken, GranularPermission.Finance_Expense_Approve);

            EnsureCanModify(actor);

            var expense = await GetExpenseAsync(
                expenseId,
                cancellationToken);

            EnsureCompanyAccess(
                actor,
                expense.CompanyId);

            await EnsureSiteAccess(
                actor,
                expense.ConstructionSiteId,
                cancellationToken);

            EnsureValidTransition(
                expense.Status,
                request.Status);

            if (request.Status == ExpenseStatus.Paid)
            {
                var settlement = await new ExpenseSettlementService(_dbContext).GetSummaryAsync(expense, cancellationToken);
                if (!settlement.IsVerified || settlement.TotalDue <= 0 || settlement.SettledAmount < settlement.TotalDue)
                    throw new ExpenseManagementException("Record a completed Payment or Petty Cash Cash Out before marking this expense as paid.");
            }

            if (request.Status == ExpenseStatus.Approved)
            {
                FinanceApprovalPolicy.EnsureIndependent(expense, currentUserId, message => new ExpenseManagementException(message));
                expense.ApprovedBy = currentUserId;
                expense.ApprovedAtUtc = DateTime.UtcNow;
            }

            expense.Status = request.Status;

            await _dbContext.SaveChangesAsync(
                cancellationToken);

            return Map(expense);

        }, cancellationToken);
    }

    private async Task<User> GetActorAsync(
        Guid currentUserId,
        CancellationToken cancellationToken,
        GranularPermission action = GranularPermission.Finance_Expense_View)
    {
        var scopedActor = await FinanceOfficerScope.ResolveActorAsync(_dbContext, currentUserId, action,
            cancellationToken, message => new ExpenseManagementException(message), supportedIndependentRoles: action == GranularPermission.Finance_Expense_Approve ? ["SuperAdmin", "CompanyAdmin", "Accountant"] : ["SuperAdmin", "CompanyAdmin", "Accountant", "SiteManager"]);
        if (scopedActor is not null) return scopedActor;
        return await _dbContext.Users
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(
                x => x.Id == currentUserId,
                cancellationToken)
            ?? throw new ExpenseManagementException(
                "Authenticated user was not found.");
    }

    private static void EnsureCanRead(User actor)
    {
        if (FinanceOfficerScope.IsOfficer(actor) || IsSuperAdmin(actor) ||
            HasRole(actor, AppRoles.CompanyAdmin) ||
            HasRole(actor, AppRoles.Accountant) ||
            HasRole(actor, AppRoles.SiteManager))
        {
            return;
        }

        throw new ExpenseManagementException(
            "You are not authorized to view expenses.");
    }

    private static void EnsureCanModify(User actor)
    {
        if (FinanceOfficerScope.IsOfficer(actor) || IsSuperAdmin(actor) ||
            HasRole(actor, AppRoles.CompanyAdmin) ||
            HasRole(actor, AppRoles.Accountant))
        {
            return;
        }

        throw new ExpenseManagementException(
            "You are not authorized to manage expenses.");
    }

    private static void EnsureCanSubmit(User actor)
    {
        if (FinanceOfficerScope.IsOfficer(actor) || IsSuperAdmin(actor) ||
            HasRole(actor, AppRoles.CompanyAdmin) ||
            HasRole(actor, AppRoles.Accountant) ||
            HasRole(actor, AppRoles.SiteManager))
        {
            return;
        }

        throw new ExpenseManagementException(
            "You are not authorized to submit expenses.");
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
            throw new ExpenseManagementException(
                "You are not authorized to access this expense.");
        }
    }

    private async Task EnsureSiteAccess(
        User actor,
        Guid? siteId,
        CancellationToken cancellationToken)
    {
        if (SiteFinanceScope.IsSiteLimited(actor))
        {
            if (!await SiteFinanceScope.HasSiteAsync(_dbContext, actor, siteId, cancellationToken))
                throw new ExpenseManagementException("You are not authorized to access this expense scope.");
            return;
        }

        // Company-level expense
        // SiteManager cannot access company-level expenses.
        if (!siteId.HasValue)
        {
            if (SiteFinanceScope.IsSiteLimited(actor))
            {
                throw new ExpenseManagementException(
                    "You are not authorized to access this expense.");
            }

            return;
        }

        // SuperAdmin has unrestricted access.
        if (IsSuperAdmin(actor))
        {
            return;
        }

        // CompanyAdmin and Accountant have company-wide finance access.
        if (HasRole(actor, AppRoles.CompanyAdmin) ||
            HasRole(actor, AppRoles.Accountant))
        {
            return;
        }

        var companyId = GetActorCompanyId(actor);

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
            throw new ExpenseManagementException(
                "You are not assigned to this site's expense scope.");
        }
    }



    private async Task<Guid> ResolveCreateCompanyAsync(
        User actor,
        Guid costCodeId,
        CancellationToken cancellationToken)
    {
        if (!IsSuperAdmin(actor))
            return GetActorCompanyId(actor);

        // The required cost code identifies the selected expense company.
        // All remaining references are validated against this company.
        return await _dbContext.CostCodes
            .Where(x => x.Id == costCodeId && !x.IsDeleted)
            .Select(x => (Guid?)x.CompanyId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ExpenseManagementException("Cost code was not found.");
    }

    private static Guid GetActorCompanyId(
        User actor)
    {
        if (!actor.CompanyId.HasValue)
        {
            throw new ExpenseManagementException(
                "Current user is not assigned to a company.");
        }

        return actor.CompanyId.Value;
    }

    private async Task<Expense> GetExpenseAsync(
        Guid expenseId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Expenses
            .SingleOrDefaultAsync(
                x =>
                    x.Id == expenseId &&
                    !x.IsDeleted,
                cancellationToken)
            ?? throw new ExpenseManagementException(
                "Expense was not found.");
    }

    private async Task EnsureReferencesBelongToCompanyAsync(
        Guid companyId,
        Guid? constructionSiteId,
        Guid? projectId,
        Guid costCodeId,
        Guid? vendorId,
        CancellationToken cancellationToken)
    {
        if (projectId.HasValue && !constructionSiteId.HasValue)
        {
            throw new ExpenseManagementException(
                "A project cannot be selected without a construction site.");
        }

        if (constructionSiteId.HasValue)
        {
            var siteExists = await _dbContext.ConstructionSites
                .AnyAsync(
                    x =>
                        x.Id == constructionSiteId.Value &&
                        x.CompanyId == companyId &&
                        !x.IsDeleted,
                    cancellationToken);

            if (!siteExists)
            {
                throw new ExpenseManagementException(
                    "Construction site was not found or does not belong to the current company.");
            }
        }

        var costCodeExists = await _dbContext.CostCodes
            .AnyAsync(
                x =>
                    x.Id == costCodeId &&
                    x.CompanyId == companyId &&
                    !x.IsDeleted,
                cancellationToken);

        if (!costCodeExists)
        {
            throw new ExpenseManagementException(
                "Cost code was not found or does not belong to the current company.");
        }

        if (projectId.HasValue)
        {
            if (!constructionSiteId.HasValue)
            {
                throw new ExpenseManagementException(
                    "A project cannot be selected without a construction site.");
            }

            var projectExists = await _dbContext.Projects
                .AnyAsync(
                    x =>
                        x.Id == projectId.Value &&
                        x.CompanyId == companyId &&
                        !x.IsDeleted &&
                        x.ConstructionSiteId == constructionSiteId.Value,
                    cancellationToken);

            if (!projectExists)
            {
                throw new ExpenseManagementException(
                    "Project was not found, does not belong to the selected construction site, or does not belong to the current company.");
            }
        }

        if (vendorId.HasValue)
        {
            var vendorExists = await _dbContext.Vendors
                .AnyAsync(
                    x =>
                        x.Id == vendorId.Value &&
                        x.CompanyId == companyId &&
                        !x.IsDeleted,
                    cancellationToken);

            if (!vendorExists)
            {
                throw new ExpenseManagementException(
                    "Vendor was not found or does not belong to the current company.");
            }
        }
    }


    private static void EnsureEditable(
        Expense expense)
    {
        if (expense.Status is not (ExpenseStatus.Draft or ExpenseStatus.Submitted or ExpenseStatus.Rejected))
        {
            throw new ExpenseManagementException(
                $"Expense cannot be modified when status is {expense.Status}.");
        }
    }

    private static void EnsureValidTransition(
        ExpenseStatus currentStatus,
        ExpenseStatus requestedStatus)
    {
        var valid = currentStatus switch
        {
            ExpenseStatus.Draft =>
                requestedStatus is
                    ExpenseStatus.Submitted or
                    ExpenseStatus.Cancelled,

            ExpenseStatus.Submitted =>
                requestedStatus is
                    ExpenseStatus.Approved or
                    ExpenseStatus.Rejected or
                    ExpenseStatus.Cancelled,

            ExpenseStatus.Approved =>
                requestedStatus is
                    ExpenseStatus.Paid,

            ExpenseStatus.Rejected =>
                requestedStatus is
                    ExpenseStatus.Submitted or
                    ExpenseStatus.Cancelled,

            ExpenseStatus.Paid => false,

            ExpenseStatus.Cancelled => false,

            _ => false
        };

        if (!valid)
        {
            throw new ExpenseManagementException(
                $"Invalid expense status transition: {currentStatus} -> {requestedStatus}.");
        }
    }

    private static void ValidateAmounts(
        decimal amount,
        decimal taxAmount,
        decimal exchangeRate)
    {
        if (amount < 0)
        {
            throw new ExpenseManagementException(
                "Expense amount cannot be negative.");
        }

        if (taxAmount < 0)
        {
            throw new ExpenseManagementException(
                "Tax amount cannot be negative.");
        }

        if (exchangeRate <= 0)
        {
            throw new ExpenseManagementException(
                "Exchange rate must be greater than zero.");
        }
    }

    private static string CleanRequired(
        string? value,
        string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ExpenseManagementException(
                $"{fieldName} is required.");
        }

        return value.Trim();
    }

    private static string? Clean(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static string CleanCurrencyCode(
        string? value)
    {
        var currency = CleanRequired(
            value,
            "Currency code")
            .ToUpperInvariant();

        if (currency.Length != 3)
        {
            throw new ExpenseManagementException(
                "Currency code must contain exactly 3 characters.");
        }

        return currency;
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

    private static ExpenseResponse Map(
        Expense expense,
        string? approvedByName = null)
    {
        return new ExpenseResponse(
            expense.Id,
            expense.CompanyId,
            expense.ConstructionSiteId,
            expense.ProjectId,
            expense.CostCodeId,
            expense.VendorId,
            expense.ExpenseNumber,
            expense.ExpenseDate,
            expense.Description,
            expense.Amount,
            expense.TaxAmount,
            expense.CurrencyCode,
            expense.ExchangeRate,
            expense.ReferenceNumber,
            expense.ReceiptDocumentUrl,
            expense.Status,
            expense.ApprovedBy,
            approvedByName,
            expense.ApprovedAtUtc,
            expense.CreatedAtUtc,
            expense.UpdatedAtUtc);
    }
}

