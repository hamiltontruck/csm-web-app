using CSM.Application.Common.Exceptions;
using CSM.Application.Finance.Payments;
using CSM.Application.Finance.Payments.Dtos;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Finance;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public sealed class PaymentService : IPaymentService
{
    private readonly ApplicationDbContext _dbContext;

    public PaymentService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<PaymentResponse>> GetAllAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanRead(actor);

        IQueryable<Payment> query =
            _dbContext.Payments.AsNoTracking();

        if (!IsSuperAdmin(actor))
        {
            var companyId = GetActorCompanyId(actor);

            query = query.Where(
                x => x.CompanyId == companyId);
        }

        var payments = await query
            .OrderByDescending(x => x.PaymentDate)
            .ThenBy(x => x.PaymentNumber)
            .ToListAsync(cancellationToken);

        return payments
            .Select(Map)
            .ToArray();
    }

    public async Task<PaymentResponse> GetByIdAsync(
        Guid currentUserId,
        Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanRead(actor);

        var payment = await GetPaymentAsync(
            paymentId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            payment.CompanyId);

        await EnsureSiteAccess(
            actor,
            payment.ConstructionSiteId,
            cancellationToken);

        return Map(payment);
    }

    public async Task<PaymentResponse> CreateAsync(
        Guid currentUserId,
        CreatePaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanModify(actor);

        var companyId = GetActorCompanyId(actor);

        // Read existing PAY-5-### numbers (including soft-deleted)
        var existingNumbers = await _dbContext.Payments
            .IgnoreQueryFilters()
            .Where(x =>
                x.CompanyId == companyId &&
                x.PaymentNumber.StartsWith("PAY-5-"))
            .Select(x => x.PaymentNumber)
            .ToListAsync(cancellationToken);

        // Highest PAY-5 sequence
        var sequence = existingNumbers
            .Select(number =>
            {
                var suffix = number.Substring("PAY-5-".Length);

                return int.TryParse(suffix, out var value)
                    ? value
                    : 0;
            })
            .DefaultIfEmpty(0)
            .Max();

        // Use the payment number entered by the user
        // Use the payment number entered by the user
        var paymentNumber = request.PaymentNumber.Trim();

        if (string.IsNullOrWhiteSpace(paymentNumber))
            throw new PaymentManagementException("Payment Number is required.");

        // Check for duplicate payment numbers
        var exists = await _dbContext.Payments
            .IgnoreQueryFilters()
            .AnyAsync(x =>
                x.CompanyId == companyId &&
                x.PaymentNumber == paymentNumber,
                cancellationToken);

        if (exists)
            throw new PaymentManagementException(
                $"Payment Number '{paymentNumber}' already exists.");

        Console.WriteLine($"Generated Payment Number = {paymentNumber}");

        var payment = new Payment
        {
            CompanyId = companyId,
            ConstructionSiteId = request.ConstructionSiteId,
            ProjectId = request.ProjectId,
            InvoiceId = request.InvoiceId,
            VendorId = request.VendorId,
            PayrollRecordId = request.PayrollRecordId,

            // Manual payment number
            PaymentNumber = paymentNumber,

            PaymentDate = request.PaymentDate,
            Direction = request.Direction,
            Amount = request.Amount,
            CurrencyCode = CleanCurrencyCode(request.CurrencyCode),
            ExchangeRate = request.ExchangeRate,
            PaymentMethod = Clean(request.PaymentMethod),
            ReferenceNumber = Clean(request.ReferenceNumber),
            Notes = Clean(request.Notes),

            Status = PaymentStatus.Pending
        };

        _dbContext.Payments.Add(payment);

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (payment.InvoiceId.HasValue)
        {
            await RecalculateInvoiceStatusAsync(
                payment.InvoiceId.Value,
                cancellationToken);
        }

        return Map(payment);
    }

    public async Task<PaymentResponse> UpdateAsync(
        Guid currentUserId,
        Guid paymentId,
        UpdatePaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanModify(actor);

        var payment = await GetPaymentAsync(
            paymentId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            payment.CompanyId);

        EnsureEditable(payment);

        ValidatePayment(
            request.PaymentDate,
            request.Amount,
            request.ExchangeRate,
            request.Direction);

        await EnsureReferencesAsync(
            payment.CompanyId,
            request.ConstructionSiteId,
            request.ProjectId,
            request.InvoiceId,
            request.VendorId,
            request.PayrollRecordId,
            request.Direction,
            cancellationToken);

        payment.ConstructionSiteId =
            request.ConstructionSiteId;

        payment.ProjectId =
            request.ProjectId;
        payment.InvoiceId = request.InvoiceId;
        payment.VendorId = request.VendorId;
        payment.PayrollRecordId =
            request.PayrollRecordId;
        payment.PaymentDate = request.PaymentDate;
        payment.Direction = request.Direction;
        payment.Amount = request.Amount;
        payment.CurrencyCode =
            CleanCurrencyCode(
                request.CurrencyCode);
        payment.ExchangeRate =
            request.ExchangeRate;
        payment.PaymentMethod =
            Clean(request.PaymentMethod);
        payment.ReferenceNumber =
            Clean(request.ReferenceNumber);
        payment.Notes =
            Clean(request.Notes);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(payment);
    }

    public async Task<PaymentResponse> ChangeStatusAsync(
        Guid currentUserId,
        Guid paymentId,
        ChangePaymentStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanModify(actor);

        var payment = await GetPaymentAsync(
            paymentId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            payment.CompanyId);

        EnsureValidTransition(
            payment.Status,
            request.Status);

        payment.Status = request.Status;

        if (request.Status == PaymentStatus.Completed &&
            payment.PayrollRecordId.HasValue)
        {
            await SynchronizePayrollStatusAsync(
                payment.PayrollRecordId.Value,
                payment.CompanyId,
                payment.PaymentNumber,
                cancellationToken);
        }

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        if (payment.InvoiceId.HasValue)
        {
            await SynchronizeInvoiceStatusAsync(
                payment.InvoiceId.Value,
                cancellationToken);
        }

        return Map(payment);
    }

    private async Task SynchronizePayrollStatusAsync(
        Guid payrollRecordId,
        Guid companyId,
        string paymentReference,
        CancellationToken cancellationToken)
    {
        var payroll = await _dbContext.PayrollRecords
            .SingleOrDefaultAsync(
                x =>
                    x.Id == payrollRecordId &&
                    x.CompanyId == companyId &&
                    !x.IsDeleted,
                cancellationToken);

        if (payroll is null)
        {
            throw new PaymentManagementException(
                "Linked payroll record was not found or does not belong to the current company.");
        }

        if (payroll.Status != PayrollStatus.Approved)
        {
            throw new PaymentManagementException(
                "Linked payroll must be approved before the payment can be completed.");
        }

        payroll.Status = PayrollStatus.Paid;
        payroll.PaidAtUtc = DateTime.UtcNow;
        payroll.PaymentReference = paymentReference;
    }

    private async Task RecalculateInvoiceStatusAsync(
        Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        var invoice = await _dbContext.Invoices
            .FirstAsync(
                x => x.Id == invoiceId,
                cancellationToken);

        var paid = await _dbContext.Payments
            .Where(x =>
                x.InvoiceId == invoiceId &&
                x.Status == PaymentStatus.Completed &&
                !x.IsDeleted)
            .SumAsync(
                x => (decimal?)x.Amount,
                cancellationToken) ?? 0m;

        // Update the financial values
        invoice.PaidAmount = paid;

        // Recalculate the status
        invoice.Status =
            paid <= 0m
                ? InvoiceStatus.Issued
                : paid < invoice.TotalAmount
                    ? InvoiceStatus.PartiallyPaid
                    : InvoiceStatus.Paid;

        invoice.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
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
            ?? throw new PaymentManagementException(
                "Authenticated user was not found.");
    }

    public async Task CompletePaymentAsync(
        Guid currentUserId,
        Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanModify(actor);

        var payment = await GetPaymentAsync(
            paymentId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            payment.CompanyId);

        EnsureValidTransition(
            payment.Status,
            PaymentStatus.Completed);

        payment.Status = PaymentStatus.Completed;
        payment.UpdatedAtUtc = DateTime.UtcNow;

        if (payment.PayrollRecordId.HasValue)
        {
            await SynchronizePayrollStatusAsync(
                payment.PayrollRecordId.Value,
                payment.CompanyId,
                PaymentStatus.Completed.ToString(),
                cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (payment.InvoiceId.HasValue)
        {
            await RecalculateInvoiceStatusAsync(
                payment.InvoiceId.Value);
        }
    }

    public async Task ReversePaymentAsync(
        Guid currentUserId,
        Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanModify(actor);

        var payment = await GetPaymentAsync(
            paymentId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            payment.CompanyId);

        EnsureValidTransition(
            payment.Status,
            PaymentStatus.Reversed);

        payment.Status = PaymentStatus.Reversed;
        payment.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (payment.InvoiceId.HasValue)
        {
            await RecalculateInvoiceStatusAsync(
                payment.InvoiceId.Value);
        }
    }

    public async Task DeleteAsync(
        Guid currentUserId,
        Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(currentUserId, cancellationToken);

        EnsureCanModify(actor);

        var payment = await GetPaymentAsync(paymentId, cancellationToken);

        EnsureCompanyAccess(actor, payment.CompanyId);

        if (payment.Status != PaymentStatus.Pending)
        {
            throw new PaymentManagementException(
                "Only recorded payments can be deleted.");
        }

        payment.IsDeleted = true;
        payment.DeletedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (payment.InvoiceId.HasValue)
        {
            await SynchronizeInvoiceStatusAsync(
                payment.InvoiceId.Value,
                cancellationToken);
        }
    }

    public async Task<IReadOnlyCollection<PaymentResponse>> GetCompanyPaymentsAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(currentUserId, cancellationToken);
        var companyId = GetActorCompanyId(actor);

        var payments = await _dbContext.Payments
            .AsNoTracking()
            .Include(p => p.Invoice)
            .Where(p =>
                p.CompanyId == companyId &&
                p.Invoice!.ConstructionSiteId == null &&
                !p.IsDeleted)
            .OrderByDescending(p => p.PaymentDate)
            .ToListAsync(cancellationToken);

        return payments.Select(Map).ToList();
    }



    private static void EnsureCanRead(User actor)
    {
        if (IsSuperAdmin(actor) ||
            HasRole(actor, AppRoles.CompanyAdmin) ||
            HasRole(actor, AppRoles.Accountant) ||
            HasRole(actor, AppRoles.SiteManager))
        {
            return;
        }

        throw new PaymentManagementException(
            "You are not authorized to view payments.");
    }

    private static void EnsureCanModify(
        User actor)
    {
        if (IsSuperAdmin(actor) ||
            HasRole(actor, AppRoles.CompanyAdmin) ||
            HasRole(actor, AppRoles.Accountant))
        {
            return;
        }

        throw new PaymentManagementException(
            "You are not authorized to manage payments.");
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
            throw new PaymentManagementException(
                "You are not authorized to access this payment.");
        }
    }

    private async Task EnsureSiteAccess(
        User actor,
        Guid? siteId,
        CancellationToken cancellationToken)
    {
        // Company-level payment
        // SiteManager cannot access company-level payments.
        if (!siteId.HasValue)
        {
            if (HasRole(actor, AppRoles.SiteManager))
            {
                throw new PaymentManagementException(
                    "You are not authorized to access this payment.");
            }

            return;
        }

        // SuperAdmin has unrestricted access.
        if (IsSuperAdmin(actor))
            return;

        // CompanyAdmin and Accountant have company-wide finance access.
        if (HasRole(actor, AppRoles.CompanyAdmin) ||
            HasRole(actor, AppRoles.Accountant))
        {
            return;
        }

        var companyId = GetActorCompanyId(actor);

        var hasSiteAccess = await _dbContext.UserSiteAccesses
            .AnyAsync(
                x => x.UserId == actor.Id &&
                     x.SiteId == siteId.Value &&
                     x.CompanyId == companyId &&
                     x.IsActive,
                cancellationToken);

        if (!hasSiteAccess)
        {
            throw new PaymentManagementException(
                "You are not assigned to this site's payment scope.");
        }
    }

    private static Guid GetActorCompanyId(
        User actor)
    {
        if (!actor.CompanyId.HasValue)
        {
            throw new PaymentManagementException(
                "Current user is not assigned to a company.");
        }

        return actor.CompanyId.Value;
    }

    private async Task<Payment> GetPaymentAsync(
        Guid paymentId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Payments
            .SingleOrDefaultAsync(
                x =>
                    x.Id == paymentId &&
                    !x.IsDeleted,
                cancellationToken)
            ?? throw new PaymentManagementException(
                "Payment was not found.");
    }

    private async Task EnsureReferencesAsync(
        Guid companyId,
        Guid? constructionSiteId,
        Guid? projectId,
        Guid? invoiceId,
        Guid? vendorId,
        Guid? payrollRecordId,
        PaymentDirection direction,
        CancellationToken cancellationToken)
    {
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
                throw new PaymentManagementException(
                    "Construction site was not found or does not belong to the current company.");
            }
        }
        if (projectId.HasValue)
        {
            var projectExists = await _dbContext.Projects
                .AnyAsync(
                    x =>
                        x.Id == projectId.Value &&
                        x.CompanyId == companyId &&
                        !x.IsDeleted,
                    cancellationToken);

            if (!projectExists)
            {
                throw new PaymentManagementException(
                    "Project was not found or does not belong to the current company.");
            }
        }

        Invoice? invoice = null;

        if (invoiceId.HasValue)
        {
            invoice = await _dbContext.Invoices
                .SingleOrDefaultAsync(
                    x =>
                        x.Id == invoiceId.Value &&
                        x.CompanyId == companyId &&
                        !x.IsDeleted,
                    cancellationToken);

            if (invoice is null)
            {
                throw new PaymentManagementException(
                    "Invoice was not found or does not belong to the current company.");
            }

            if (invoice.Status is
                InvoiceStatus.Cancelled or
                InvoiceStatus.Paid)
            {
                throw new PaymentManagementException(
                    $"Payments cannot be added to an invoice with status {invoice.Status}.");
            }

            var paymentDirection = (PaymentDirection)direction;

            if (invoice.Type == InvoiceType.ClientInvoice &&
                direction != PaymentDirection.Incoming)
            {
                throw new PaymentManagementException(
                    "Client Invoice payments must use Incoming direction.");
            }

            if (invoice.Type == InvoiceType.VendorBill &&
                direction != PaymentDirection.Outgoing)
            {
                throw new PaymentManagementException(
                    "Vendor Bill payments must use Outgoing direction.");
            }

            if (invoice.Type == InvoiceType.VendorBill)
            {
                if (!invoice.VendorId.HasValue)
                {
                    throw new PaymentManagementException(
                        "Vendor Bill does not have an associated vendor.");
                }

                if (vendorId.HasValue &&
                    vendorId.Value != invoice.VendorId.Value)
                {
                    throw new PaymentManagementException(
                        "Payment vendor must match the Vendor Bill vendor.");
                }
            }

            if (invoice.Type == InvoiceType.ClientInvoice &&
                vendorId.HasValue)
            {
                throw new PaymentManagementException(
                    "Client Invoice payments must not specify a vendor.");
            }

            if (invoice.ConstructionSiteId != constructionSiteId)
            {
                throw new PaymentManagementException(
                    "Payment construction site must match the invoice construction site.");
            }

            if (invoice.ProjectId.HasValue &&
                projectId.HasValue &&
                invoice.ProjectId.Value != projectId.Value)
            {
                throw new PaymentManagementException(
                    "Payment project must match the invoice project.");
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
                throw new PaymentManagementException(
                    "Vendor was not found or does not belong to the current company.");
            }
        }

        if (payrollRecordId.HasValue)
        {
            if (invoiceId.HasValue || vendorId.HasValue)
            {
                throw new PaymentManagementException(
                    "Payroll payments cannot specify an invoice or vendor.");
            }

            var payroll = await _dbContext.PayrollRecords
                .SingleOrDefaultAsync(
                    x =>
                        x.Id == payrollRecordId.Value &&
                        x.CompanyId == companyId &&
                        !x.IsDeleted,
                    cancellationToken);

            if (payroll is null)
            {
                throw new PaymentManagementException(
                    "Payroll record was not found or does not belong to the current company.");
            }

            if (payroll.Status != PayrollStatus.Approved)
            {
                throw new PaymentManagementException(
                    "Only approved payroll can be paid.");
            }
        }

        if (!invoiceId.HasValue &&
            !vendorId.HasValue &&
            !payrollRecordId.HasValue)
        {
            throw new PaymentManagementException(
                "Payment must reference an invoice, vendor, or payroll record.");
        }
    }

    private static void ValidatePayment(
        DateOnly paymentDate,
        decimal amount,
        decimal exchangeRate,
        PaymentDirection direction)
    {
        if (!Enum.IsDefined(direction))
        {
            throw new PaymentManagementException(
                "Payment direction is invalid.");
        }

        if (amount <= 0)
        {
            throw new PaymentManagementException(
                "Payment amount must be greater than zero.");
        }

        if (exchangeRate <= 0)
        {
            throw new PaymentManagementException(
                "Exchange rate must be greater than zero.");
        }

        if (paymentDate > DateOnly.FromDateTime(
                DateTime.UtcNow.Date))
        {
            throw new PaymentManagementException(
                "Payment date cannot be in the future.");
        }
    }

    private static void EnsureEditable(
        Payment payment)
    {
        if (payment.Status != PaymentStatus.Pending)
        {
            throw new PaymentManagementException(
                $"Payment cannot be modified when status is {payment.Status}.");
        }
    }

    private static void EnsureValidTransition(
        PaymentStatus currentStatus,
        PaymentStatus requestedStatus)
    {
        var valid = currentStatus switch
        {
            PaymentStatus.Pending =>
                requestedStatus is
                    PaymentStatus.Completed or
                    PaymentStatus.Failed or
                    PaymentStatus.Cancelled,

            PaymentStatus.Completed =>
                requestedStatus is
                    PaymentStatus.Reversed,

            PaymentStatus.Failed => false,

            PaymentStatus.Cancelled => false,

            PaymentStatus.Reversed => false,

            _ => false
        };

        if (!valid)
        {
            throw new PaymentManagementException(
                $"Invalid payment status transition: {currentStatus} -> {requestedStatus}.");
        }
    }

    private async Task SynchronizeInvoiceStatusAsync(
        Guid invoiceId,
        CancellationToken cancellationToken)
    {
        var invoice = await _dbContext.Invoices
            .SingleOrDefaultAsync(
                x =>
                    x.Id == invoiceId &&
                    !x.IsDeleted,
                cancellationToken);

        if (invoice is null)
        {
            return;
        }

        var completedPayments = await _dbContext.Payments
            .Where(
                x =>
                    x.InvoiceId == invoiceId &&
                    x.Status == PaymentStatus.Completed &&
                    !x.IsDeleted)
            .SumAsync(
                x => (decimal?)x.Amount,
                cancellationToken)
            ?? 0m;

        if (completedPayments <= 0)
        {
            if (invoice.Status != InvoiceStatus.Cancelled)
            {
                invoice.Status =
                    InvoiceStatus.Issued;
            }
        }
        else if (completedPayments < invoice.TotalAmount)
        {
            invoice.Status =
                InvoiceStatus.PartiallyPaid;
        }
        else
        {
            invoice.Status =
                InvoiceStatus.Paid;
        }

        await _dbContext.SaveChangesAsync(
            cancellationToken);
    }

    private static string CleanRequired(
        string? value,
        string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new PaymentManagementException(
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
            throw new PaymentManagementException(
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

    private static PaymentResponse Map(
        Payment payment)
    {
        return new PaymentResponse(
            payment.Id,
            payment.CompanyId,
            payment.ConstructionSiteId,
            payment.ProjectId,
            payment.InvoiceId,
            payment.Invoice?.InvoiceNumber,
            payment.VendorId,
            payment.PayrollRecordId,
            payment.PaymentNumber,
            payment.PaymentDate,
            payment.Direction,
            payment.Amount,
            payment.CurrencyCode,
            payment.ExchangeRate,
            payment.PaymentMethod,
            payment.ReferenceNumber,
            payment.Notes,
            payment.Status,
            payment.CreatedAtUtc,
            payment.UpdatedAtUtc);
    }
}
