using CSM.Application.Common.Exceptions;
using CSM.Application.Finance.Invoices;
using CSM.Application.Finance.Invoices.Dtos;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Finance;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using CSM.Application.Finance.Payments.Dtos;

namespace CSM.Infrastructure.Services;

public sealed class InvoiceService : IInvoiceService
{
    private readonly ApplicationDbContext _dbContext;

    public InvoiceService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<InvoiceResponse>> GetAllAsync(
        Guid currentUserId,
        Guid siteId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanRead(actor);

        IQueryable<Invoice> query = _dbContext.Invoices
            .AsNoTracking()
            .Include(x => x.Project)
            .Where(x => x.Project != null &&
                        x.Project.ConstructionSiteId == siteId);

        if (!IsSuperAdmin(actor))
        {
            var companyId = GetActorCompanyId(actor);

            query = query.Where(x => x.CompanyId == companyId);
        }

        var invoices = await query
            .OrderByDescending(x => x.InvoiceDate)
            .ThenBy(x => x.InvoiceNumber)
            .ToListAsync(cancellationToken);

        return invoices
            .Select(invoice => Map(invoice))
            .ToArray();
    }

    public async Task<IReadOnlyCollection<InvoiceResponse>> GetCompanyInvoicesAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(currentUserId, cancellationToken);
        var companyId = GetActorCompanyId(actor);

        var invoices = await _dbContext.Invoices
            .AsNoTracking()
            .Include(i => i.Vendor)
            .Include(i => i.Project)
            .Where(i =>
                i.CompanyId == companyId &&
                i.ConstructionSiteId == null &&
                !i.IsDeleted)
            .OrderByDescending(i => i.InvoiceDate)
            .ToListAsync(cancellationToken);

        return invoices
            .Select(i => Map(i))
            .ToList();
    }

    public async Task<InvoiceResponse> GetByIdAsync(
        Guid currentUserId,
        Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanRead(actor);

        var invoice = await GetInvoiceAsync(
            invoiceId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            invoice.CompanyId);

        var paidAmount = invoice.Payments
            .Where(p => p.Status == PaymentStatus.Completed)
            .Sum(p => p.Amount);

        return Map(invoice, paidAmount);
    }

    public async Task<InvoiceResponse> CreateAsync(
        Guid currentUserId,
        CreateInvoiceRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanModify(actor);

        var companyId = GetActorCompanyId(actor);

        var invoiceNumber = CleanRequired(
            request.InvoiceNumber,
            "Invoice number");

        ValidateInvoice(
            request.InvoiceDate,
            request.DueDate,
            request.Subtotal,
            request.TaxAmount,
            request.TotalAmount,
            request.ExchangeRate,
            request.Type);

        await EnsureReferencesBelongToCompanyAsync(
            companyId,
            request.ConstructionSiteId,
            request.ProjectId,
            request.VendorId,
            request.Type,
            cancellationToken);

        var duplicateExists = await _dbContext.Invoices
            .AnyAsync(
                x =>
                    x.CompanyId == companyId &&
                    x.InvoiceNumber == invoiceNumber &&
                    !x.IsDeleted,
                cancellationToken);

        if (duplicateExists)
        {
            throw new InvoiceManagementException(
                $"Invoice number '{invoiceNumber}' already exists.");
        }

        var invoice = new Invoice
        {
            CompanyId = companyId,
            ConstructionSiteId =
                request.ConstructionSiteId,
            ProjectId = request.ProjectId,
            VendorId = request.VendorId,
            InvoiceNumber = invoiceNumber,
            Type = request.Type,
            InvoiceDate = request.InvoiceDate,
            DueDate = request.DueDate,
            Subtotal = request.Subtotal,
            TaxAmount = request.TaxAmount,
            TotalAmount = request.TotalAmount,
            CurrencyCode = CleanCurrencyCode(
                request.CurrencyCode),
            ExchangeRate = request.ExchangeRate,
            Status = InvoiceStatus.Draft,
            Description = Clean(request.Description),
            ExternalReference =
                Clean(request.ExternalReference)
        };

        _dbContext.Invoices.Add(invoice);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(invoice);
    }

    public async Task<InvoiceResponse> UpdateAsync(
        Guid currentUserId,
        Guid invoiceId,
        UpdateInvoiceRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanModify(actor);

        var invoice = await GetInvoiceAsync(
            invoiceId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            invoice.CompanyId);

        EnsureEditable(invoice);

        ValidateInvoice(
            request.InvoiceDate,
            request.DueDate,
            request.Subtotal,
            request.TaxAmount,
            request.TotalAmount,
            request.ExchangeRate,
            request.Type);

        await EnsureReferencesBelongToCompanyAsync(
            invoice.CompanyId,
            request.ConstructionSiteId,
            request.ProjectId,
            request.VendorId,
            request.Type,
            cancellationToken);

        invoice.ConstructionSiteId =
            request.ConstructionSiteId;
        invoice.ProjectId =
            request.ProjectId;
        invoice.VendorId =
            request.VendorId;
        invoice.Type =
            request.Type;
        invoice.InvoiceDate =
            request.InvoiceDate;
        invoice.DueDate =
            request.DueDate;
        invoice.Subtotal =
            request.Subtotal;
        invoice.TaxAmount =
            request.TaxAmount;
        invoice.TotalAmount =
            request.TotalAmount;
        invoice.CurrencyCode =
            CleanCurrencyCode(
                request.CurrencyCode);
        invoice.ExchangeRate =
            request.ExchangeRate;
        invoice.Description =
            Clean(request.Description);
        invoice.ExternalReference =
            Clean(request.ExternalReference);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(invoice);
    }

    public async Task<InvoiceResponse> ChangeStatusAsync(
        Guid currentUserId,
        Guid invoiceId,
        ChangeInvoiceStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanModify(actor);

        var invoice = await GetInvoiceAsync(
            invoiceId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            invoice.CompanyId);

        EnsureValidTransition(
            invoice.Status,
            request.Status);

        invoice.Status = request.Status;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(invoice);
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
            ?? throw new InvoiceManagementException(
                "Authenticated user was not found.");
    }

    private static void EnsureCanRead(
        User actor)
    {
        if (IsSuperAdmin(actor) ||
            HasRole(actor, AppRoles.CompanyAdmin) ||
            HasRole(actor, AppRoles.Accountant))
        {
            return;
        }

        throw new InvoiceManagementException(
            "You are not authorized to view invoices.");
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

        throw new InvoiceManagementException(
            "You are not authorized to manage invoices.");
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
            throw new InvoiceManagementException(
                "You are not authorized to access this invoice.");
        }
    }

    private static Guid GetActorCompanyId(
        User actor)
    {
        if (!actor.CompanyId.HasValue)
        {
            throw new InvoiceManagementException(
                "Current user is not assigned to a company.");
        }

        return actor.CompanyId.Value;
    }

    private async Task<Invoice> GetInvoiceAsync(
        Guid invoiceId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Invoices
            .Include(x => x.Payments)
            .SingleOrDefaultAsync(
                x => x.Id == invoiceId &&
                     !x.IsDeleted,
                cancellationToken)
            ?? throw new InvoiceManagementException(
                "Invoice was not found.");
    }

    private async Task<decimal> GetPaidAmountAsync(
        Guid invoiceId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Payments
            .Where(x =>
                x.InvoiceId == invoiceId &&
                x.Status == PaymentStatus.Completed &&
                !x.IsDeleted)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
    }

    private async Task EnsureReferencesBelongToCompanyAsync(
        Guid companyId,
        Guid? constructionSiteId,
        Guid? projectId,
        Guid? vendorId,
        InvoiceType type,
        CancellationToken cancellationToken)
    {
        // Construction Site (optional for company invoices)
        if (constructionSiteId.HasValue)
        {
            var siteExists = await _dbContext.ConstructionSites
                .AnyAsync(
                    x => x.Id == constructionSiteId.Value &&
                         x.CompanyId == companyId &&
                         !x.IsDeleted,
                    cancellationToken);

            if (!siteExists)
            {
                throw new InvoiceManagementException(
                    "Construction site was not found or does not belong to the current company.");
            }
        }

        // Project
        if (projectId.HasValue)
        {
            var projectExists = await _dbContext.Projects
                .AnyAsync(
                    x => x.Id == projectId.Value &&
                         x.CompanyId == companyId &&
                         !x.IsDeleted,
                    cancellationToken);

            if (!projectExists)
            {
                throw new InvoiceManagementException(
                    "Project was not found or does not belong to the current company.");
            }
        }

        // Vendor
        if (vendorId.HasValue)
        {
            var vendorExists = await _dbContext.Vendors
                .AnyAsync(
                    x => x.Id == vendorId.Value &&
                         x.CompanyId == companyId &&
                         !x.IsDeleted,
                    cancellationToken);

            if (!vendorExists)
            {
                throw new InvoiceManagementException(
                    "Vendor was not found or does not belong to the current company.");
            }
        }

        if (type == InvoiceType.VendorBill && !vendorId.HasValue)
        {
            throw new InvoiceManagementException(
                "Vendor is required for a Vendor Bill.");
        }

        if (type == InvoiceType.ClientInvoice && vendorId.HasValue)
        {
            throw new InvoiceManagementException(
                "Vendor must not be specified for a Client Invoice.");
        }
    }

    public async Task<InvoiceResponse> IssueAsync(
        Guid currentUserId,
        Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanModify(actor);

        var invoice = await GetInvoiceAsync(
            invoiceId,
            cancellationToken);

        EnsureCompanyAccess(actor, invoice.CompanyId);

        if (invoice.Status != InvoiceStatus.Draft)
            throw new InvoiceManagementException(
                "Only Draft invoices can be issued.");

        invoice.Status = InvoiceStatus.Issued;
        invoice.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var paid = await GetPaidAmountAsync(
            invoice.Id,
            cancellationToken);

        return Map(invoice, paid);
    }

    public async Task<InvoiceResponse> CancelAsync(
        Guid currentUserId,
        Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(currentUserId, cancellationToken);

        EnsureCanModify(actor);

        var invoice = await GetInvoiceAsync(invoiceId, cancellationToken);

        EnsureCompanyAccess(actor, invoice.CompanyId);

        var paid = await GetPaidAmountAsync(invoice.Id, cancellationToken);

        if (paid > 0)
            throw new InvoiceManagementException(
                "Cannot cancel an invoice that has payments.");

        if (invoice.Status == InvoiceStatus.Cancelled)
            throw new InvoiceManagementException(
                "Invoice is already cancelled.");

        invoice.Status = InvoiceStatus.Cancelled;
        invoice.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Map(invoice, paid);
    }

    private static void EnsureEditable(
        Invoice invoice)
    {
        if (invoice.Status is
            InvoiceStatus.Paid or
            InvoiceStatus.Cancelled)
        {
            throw new InvoiceManagementException(
                $"Invoice cannot be modified when status is {invoice.Status}.");
        }
    }

    private static void EnsureValidTransition(
        InvoiceStatus currentStatus,
        InvoiceStatus requestedStatus)
    {
        var valid = currentStatus switch
        {
            InvoiceStatus.Draft =>
                requestedStatus is
                    InvoiceStatus.Issued or
                    InvoiceStatus.Cancelled,

            InvoiceStatus.Issued =>
                requestedStatus is
                    InvoiceStatus.Overdue or
                    InvoiceStatus.PartiallyPaid or
                    InvoiceStatus.Paid or
                    InvoiceStatus.Cancelled,

            InvoiceStatus.Overdue =>
                requestedStatus is
                    InvoiceStatus.PartiallyPaid or
                    InvoiceStatus.Paid or
                    InvoiceStatus.Cancelled,

            InvoiceStatus.PartiallyPaid =>
                requestedStatus is
                    InvoiceStatus.Paid or
                    InvoiceStatus.Cancelled,

            InvoiceStatus.Paid => false,

            InvoiceStatus.Cancelled => false,

            _ => false
        };

        if (!valid)
        {
            throw new InvoiceManagementException(
                $"Invalid invoice status transition: {currentStatus} -> {requestedStatus}.");
        }
    }

    private static void ValidateInvoice(
        DateOnly invoiceDate,
        DateOnly? dueDate,
        decimal subtotal,
        decimal taxAmount,
        decimal totalAmount,
        decimal exchangeRate,
        InvoiceType type)
    {
        if (!Enum.IsDefined(type))
        {
            throw new InvoiceManagementException(
                "Invoice type is invalid.");
        }

        if (dueDate.HasValue &&
            dueDate.Value < invoiceDate)
        {
            throw new InvoiceManagementException(
                "Due date cannot be earlier than invoice date.");
        }

        if (subtotal < 0)
        {
            throw new InvoiceManagementException(
                "Subtotal cannot be negative.");
        }

        if (taxAmount < 0)
        {
            throw new InvoiceManagementException(
                "Tax amount cannot be negative.");
        }

        if (totalAmount < 0)
        {
            throw new InvoiceManagementException(
                "Total amount cannot be negative.");
        }

        if (totalAmount != subtotal + taxAmount)
        {
            throw new InvoiceManagementException(
                "Total amount must equal subtotal plus tax amount.");
        }

        if (exchangeRate <= 0)
        {
            throw new InvoiceManagementException(
                "Exchange rate must be greater than zero.");
        }
    }

    private static string CleanRequired(
        string? value,
        string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvoiceManagementException(
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
            throw new InvoiceManagementException(
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
    private static void UpdateInvoiceStatus(Invoice invoice)
    {
        if (invoice.PaidAmount <= 0)
        {
            invoice.Status = InvoiceStatus.Issued;
            return;
        }

        if (invoice.PaidAmount >= invoice.TotalAmount)
        {
            invoice.Status = InvoiceStatus.Paid;
            return;
        }

        invoice.Status = InvoiceStatus.PartiallyPaid;
    }



    private static InvoiceResponse Map(Invoice invoice)
    {
        var paidAmount = invoice.Payments?
            .Where(p => p.Status == PaymentStatus.Completed)
            .Sum(p => p.Amount) ?? 0m;

        return Map(invoice, paidAmount);
    }
    private static PaymentResponse MapPayment(Payment payment)
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
            payment.UpdatedAtUtc      // ← required final parameter
        );
    }

    private static InvoiceResponse Map(
        Invoice invoice,
        decimal paidAmount)
    {
        return new InvoiceResponse(
            invoice.Id,
            invoice.CompanyId,
            invoice.ConstructionSiteId,
            invoice.ProjectId,
            invoice.VendorId,
            invoice.InvoiceNumber,
            invoice.Type,
            invoice.InvoiceDate,
            invoice.DueDate,
            invoice.Subtotal,
            invoice.TaxAmount,
            invoice.TotalAmount,
            paidAmount,
            invoice.TotalAmount - paidAmount,
            invoice.CurrencyCode,
            invoice.ExchangeRate,
            invoice.Status,
            invoice.Description,
            invoice.ExternalReference,
            invoice.CreatedAtUtc,
            invoice.UpdatedAtUtc,   // ← comma required

            invoice.Payments
                .OrderByDescending(p => p.PaymentDate)
                .Select(MapPayment)
                .ToList()
        );
    }
}
