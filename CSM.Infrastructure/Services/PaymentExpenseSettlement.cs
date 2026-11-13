using CSM.Application.Common.Exceptions;
using CSM.Application.Finance.Payments.Dtos;
using CSM.Domain.Entities.Finance;
using CSM.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CSM.Infrastructure.Services;

public sealed partial class PaymentService
{
    public async Task<PaymentResponse> CreateAsync(Guid currentUserId, CreatePaymentRequest request, CancellationToken cancellationToken = default)
    {
        if (request.ExpenseId is not Guid expenseId) return await CreateCoreAsync(currentUserId, request, cancellationToken);
        return await TranslateSettlementErrorsAsync(() => FinanceMutationTransaction.ExecuteAsync(_dbContext, new(ExpenseIds: [expenseId]), async () =>
        {
            var settlement = new ExpenseSettlementService(_dbContext);
            var expense = await settlement.LoadForDisbursementAsync(currentUserId, expenseId, "payment", cancellationToken);
            EnsureExpenseContext(expense, request.ConstructionSiteId, request.ProjectId, request.VendorId, request.InvoiceId, request.PayrollRecordId, request.Direction);
            ValidatePayment(request.PaymentDate, request.Amount, request.ExchangeRate, request.Direction);
            await settlement.ValidateNewSettlementAsync(expense, request.Amount, CleanCurrencyCode(request.CurrencyCode), request.ExchangeRate, false, cancellationToken);
            var number = CleanRequired(request.PaymentNumber, "Payment number");
            if (number.Length > 50) throw new PaymentManagementException("Payment number cannot exceed 50 characters.");
            if (await _dbContext.Payments.IgnoreQueryFilters().AnyAsync(x => x.CompanyId == expense.CompanyId && x.PaymentNumber == number, cancellationToken))
                throw new PaymentManagementException("Payment number already exists.");
            var payment = new Payment
            {
                ExpenseId = expense.Id, CompanyId = expense.CompanyId, ConstructionSiteId = expense.ConstructionSiteId,
                ProjectId = expense.ProjectId, VendorId = expense.VendorId, PaymentNumber = number, PaymentDate = request.PaymentDate,
                Direction = PaymentDirection.Outgoing, Amount = request.Amount, CurrencyCode = expense.CurrencyCode, ExchangeRate = expense.ExchangeRate,
                PaymentMethod = Clean(request.PaymentMethod), ReferenceNumber = Clean(request.ReferenceNumber), Notes = Clean(request.Notes),
                Status = PaymentStatus.Pending, CreatedBy = currentUserId, UpdatedBy = currentUserId
            };
            _dbContext.Payments.Add(payment); await _dbContext.SaveChangesAsync(cancellationToken);
            return Map(payment);
        }, cancellationToken));
    }

    public Task<PaymentResponse> UpdateAsync(Guid currentUserId, Guid paymentId, UpdatePaymentRequest request, CancellationToken cancellationToken = default) =>
        MutatePaymentAsync(paymentId, request.ExpenseId, async payment =>
        {
            if (!payment.ExpenseId.HasValue && !request.ExpenseId.HasValue) return await UpdateCoreAsync(currentUserId, paymentId, request, cancellationToken);
            var actor = await GetActorAsync(currentUserId, cancellationToken, GranularPermission.Finance_Payment_Manage);
            EnsureCanModify(actor);
            EnsureCompanyAccess(actor, payment.CompanyId);
            await EnsureSiteAccess(actor, payment.ConstructionSiteId, cancellationToken);
            await EnsureOfficerReferencesAsync(actor, payment.CompanyId, payment.ConstructionSiteId, payment.ProjectId,
                payment.InvoiceId, payment.PayrollRecordId, cancellationToken);
            EnsureEditable(payment);
            var settlement = new ExpenseSettlementService(_dbContext);
            if (payment.ExpenseId is Guid oldId) await settlement.LoadForDisbursementAsync(currentUserId, oldId, "payment", cancellationToken);
            if (request.ExpenseId is not Guid newId)
            {
                // Validate the old parent before detaching; the existing direct/invoice/payroll
                // implementation validates the new parent and all references.
                var result = await UpdateCoreAsync(currentUserId, paymentId, request, cancellationToken);
                payment.ExpenseId = null; await _dbContext.SaveChangesAsync(cancellationToken); return Map(payment);
            }
            var expense = await settlement.LoadForDisbursementAsync(currentUserId, newId, "payment", cancellationToken);
            if (payment.CompanyId != expense.CompanyId) throw new PaymentManagementException("Expense must belong to the payment company.");
            EnsureExpenseContext(expense, request.ConstructionSiteId, request.ProjectId, request.VendorId, request.InvoiceId, request.PayrollRecordId, request.Direction);
            ValidatePayment(request.PaymentDate, request.Amount, request.ExchangeRate, request.Direction);
            await settlement.ValidateNewSettlementAsync(expense, request.Amount, CleanCurrencyCode(request.CurrencyCode), request.ExchangeRate, false, cancellationToken);
            payment.ExpenseId = expense.Id; payment.ConstructionSiteId = expense.ConstructionSiteId; payment.ProjectId = expense.ProjectId;
            payment.VendorId = expense.VendorId; payment.InvoiceId = null; payment.PayrollRecordId = null;
            payment.PaymentDate = request.PaymentDate; payment.Direction = request.Direction; payment.Amount = request.Amount;
            payment.CurrencyCode = expense.CurrencyCode; payment.ExchangeRate = expense.ExchangeRate;
            payment.PaymentMethod = Clean(request.PaymentMethod); payment.ReferenceNumber = Clean(request.ReferenceNumber); payment.Notes = Clean(request.Notes);
            payment.UpdatedBy = currentUserId; await _dbContext.SaveChangesAsync(cancellationToken); return Map(payment);
        }, cancellationToken);

    public Task<PaymentResponse> ChangeStatusAsync(Guid currentUserId, Guid paymentId, ChangePaymentStatusRequest request, CancellationToken cancellationToken = default) =>
        MutatePaymentAsync(paymentId, null, payment => payment.ExpenseId.HasValue
            ? ChangeExpensePaymentStatusAsync(currentUserId, payment, request.Status, cancellationToken)
            : ChangeStatusCoreAsync(currentUserId, paymentId, request, cancellationToken), cancellationToken);

    public async Task CompletePaymentAsync(Guid currentUserId, Guid paymentId, CancellationToken cancellationToken = default) =>
        await MutatePaymentAsync<object?>(paymentId, null, async payment =>
        {
            if (payment.ExpenseId.HasValue) await ChangeExpensePaymentStatusAsync(currentUserId, payment, PaymentStatus.Completed, cancellationToken);
            else await CompletePaymentCoreAsync(currentUserId, paymentId, cancellationToken);
            return null;
        }, cancellationToken);

    public async Task ReversePaymentAsync(Guid currentUserId, Guid paymentId, CancellationToken cancellationToken = default) =>
        await MutatePaymentAsync<object?>(paymentId, null, async payment =>
        {
            if (payment.ExpenseId.HasValue) await ChangeExpensePaymentStatusAsync(currentUserId, payment, PaymentStatus.Reversed, cancellationToken);
            else await ReversePaymentCoreAsync(currentUserId, paymentId, cancellationToken);
            return null;
        }, cancellationToken);

    public async Task DeleteAsync(Guid currentUserId, Guid paymentId, CancellationToken cancellationToken = default) =>
        await MutatePaymentAsync<object?>(paymentId, null, async payment =>
        {
            if (payment.ExpenseId is Guid expenseId)
                await new ExpenseSettlementService(_dbContext).LoadForDisbursementAsync(currentUserId, expenseId, "payment", cancellationToken);
            await DeleteCoreAsync(currentUserId, paymentId, cancellationToken); return null;
        }, cancellationToken);

    private async Task<PaymentResponse> ChangeExpensePaymentStatusAsync(Guid actorId, Payment payment, PaymentStatus status, CancellationToken ct)
    {
        var settlement = new ExpenseSettlementService(_dbContext);
        var expense = await settlement.LoadForDisbursementAsync(actorId, payment.ExpenseId!.Value, "payment", ct);
        EnsureValidTransition(payment.Status, status);
        if (status == PaymentStatus.Completed)
        {
            if (payment.CompanyId != expense.CompanyId) throw new PaymentManagementException("Payment company does not match the expense.");
            EnsureExpenseContext(expense, payment.ConstructionSiteId, payment.ProjectId, payment.VendorId, payment.InvoiceId, payment.PayrollRecordId, payment.Direction);
            await settlement.ValidateNewSettlementAsync(expense, payment.Amount, payment.CurrencyCode, payment.ExchangeRate, false, ct);
        }
        payment.Status = status; payment.UpdatedBy = actorId;
        await _dbContext.SaveChangesAsync(ct);
        if (status is PaymentStatus.Completed or PaymentStatus.Reversed)
        {
            await settlement.SynchronizeAsync(expense, ct);
            await _dbContext.SaveChangesAsync(ct);
        }
        return Map(payment);
    }

    private async Task<T> MutatePaymentAsync<T>(Guid paymentId, Guid? nextExpenseId, Func<Payment, Task<T>> action, CancellationToken ct)
    {
        var snapshot = await _dbContext.Payments.AsNoTracking().Where(x => x.Id == paymentId && !x.IsDeleted)
            .Select(x => new { x.ExpenseId }).SingleOrDefaultAsync(ct) ?? throw new PaymentManagementException("Payment was not found.");
        var ids = new[] { snapshot.ExpenseId, nextExpenseId }.Where(x => x.HasValue).Select(x => x!.Value).ToArray();
        return await TranslateSettlementErrorsAsync(() => FinanceMutationTransaction.ExecuteAsync(_dbContext, new(ExpenseIds: ids, PaymentId: paymentId), async () =>
        {
            var payment = await GetPaymentAsync(paymentId, ct);
            if (payment.ExpenseId != snapshot.ExpenseId) throw new PaymentManagementException("Payment source changed. Reload the payment and try again.");
            return await action(payment);
        }, ct));
    }

    private static void EnsureExpenseContext(Expense expense, Guid? site, Guid? project, Guid? vendor, Guid? invoice, Guid? payroll, PaymentDirection direction)
    {
        if (invoice.HasValue || payroll.HasValue) throw new PaymentManagementException("An expense payment cannot also reference an invoice or payroll.");
        if (direction != PaymentDirection.Outgoing || site != expense.ConstructionSiteId || project != expense.ProjectId || vendor != expense.VendorId)
            throw new PaymentManagementException("Outgoing payment site, project and vendor must match the selected expense.");
    }

    private static async Task<T> TranslateSettlementErrorsAsync<T>(Func<Task<T>> action)
    {
        try { return await action(); }
        catch (ExpenseManagementException ex) { throw new PaymentManagementException(ex.Message); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { throw new PaymentManagementException("Payment number already exists. Reload before trying again."); }
    }
}
