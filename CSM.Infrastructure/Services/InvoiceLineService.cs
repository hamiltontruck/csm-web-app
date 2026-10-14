using CSM.Application.Common.Exceptions;
using CSM.Application.Finance.InvoiceLines;
using CSM.Application.Finance.InvoiceLines.Dtos;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Finance;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public sealed class InvoiceLineService : IInvoiceLineService
{
    private readonly ApplicationDbContext _dbContext;

    public InvoiceLineService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<InvoiceLineResponse>> GetByInvoiceAsync(
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

        var lines = await _dbContext.InvoiceLines
            .AsNoTracking()
            .Where(x => x.InvoiceId == invoiceId)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return lines
            .Select(Map)
            .ToArray();
    }

    public async Task<InvoiceLineResponse> GetByIdAsync(
        Guid currentUserId,
        Guid invoiceLineId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanRead(actor);

        var line = await GetLineAsync(
            invoiceLineId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            line.CompanyId);

        return Map(line);
    }

    public async Task<InvoiceLineResponse> CreateAsync(
        Guid currentUserId,
        Guid invoiceId,
        CreateInvoiceLineRequest request,
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

        EnsureInvoiceEditable(invoice);

        ValidateLine(
            request.Description,
            request.Quantity,
            request.UnitPrice,
            request.TaxAmount);

        await EnsureCostCodeAsync(
            invoice.CompanyId,
            request.CostCodeId,
            cancellationToken);

        var description = CleanRequired(
            request.Description,
            "Invoice line description");

        var lineTotal =
            request.Quantity * request.UnitPrice
            + request.TaxAmount;

        var line = new InvoiceLine
        {
            CompanyId = invoice.CompanyId,
            InvoiceId = invoice.Id,
            CostCodeId = request.CostCodeId,
            Description = description,
            Quantity = request.Quantity,
            UnitOfMeasure = request.UnitOfMeasure,
            UnitPrice = request.UnitPrice,
            TaxAmount = request.TaxAmount,
            LineTotal = request.Quantity * request.UnitPrice + request.TaxAmount
        };

        _dbContext.InvoiceLines.Add(line);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        await RecalculateInvoiceTotalsAsync(
            invoice,
            cancellationToken);

        return Map(line);
    }

    public async Task<InvoiceLineResponse> UpdateAsync(
        Guid currentUserId,
        Guid invoiceLineId,
        UpdateInvoiceLineRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanModify(actor);

        var line = await GetLineAsync(
            invoiceLineId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            line.CompanyId);

        var invoice = await GetInvoiceAsync(
            line.InvoiceId,
            cancellationToken);

        EnsureInvoiceEditable(invoice);

        ValidateLine(
            request.Description,
            request.Quantity,
            request.UnitPrice,
            request.TaxAmount);

        await EnsureCostCodeAsync(
            invoice.CompanyId,
            request.CostCodeId,
            cancellationToken);

        line.CostCodeId = request.CostCodeId;
        line.Description = CleanRequired(
            request.Description,
            "Invoice line description");
        line.Quantity = request.Quantity;
        line.UnitOfMeasure = request.UnitOfMeasure;
        line.UnitPrice = request.UnitPrice;
        line.TaxAmount = request.TaxAmount;
        line.LineTotal =
            request.Quantity * request.UnitPrice
            + request.TaxAmount;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        await RecalculateInvoiceTotalsAsync(
            invoice,
            cancellationToken);

        return Map(line);
    }

    public async Task DeleteAsync(
        Guid currentUserId,
        Guid invoiceLineId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanModify(actor);

        var line = await GetLineAsync(
            invoiceLineId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            line.CompanyId);

        var invoice = await GetInvoiceAsync(
            line.InvoiceId,
            cancellationToken);

        EnsureInvoiceEditable(invoice);

        line.IsDeleted = true;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        await RecalculateInvoiceTotalsAsync(
            invoice,
            cancellationToken);
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
            ?? throw new InvoiceLineManagementException(
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

        throw new InvoiceLineManagementException(
            "You are not authorized to view invoice lines.");
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

        throw new InvoiceLineManagementException(
            "You are not authorized to manage invoice lines.");
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
            throw new InvoiceLineManagementException(
                "You are not authorized to access this invoice line.");
        }
    }

    private static Guid GetActorCompanyId(
        User actor)
    {
        if (!actor.CompanyId.HasValue)
        {
            throw new InvoiceLineManagementException(
                "Current user is not assigned to a company.");
        }

        return actor.CompanyId.Value;
    }

    private async Task<Invoice> GetInvoiceAsync(
        Guid invoiceId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Invoices
            .SingleOrDefaultAsync(
                x =>
                    x.Id == invoiceId &&
                    !x.IsDeleted,
                cancellationToken)
            ?? throw new InvoiceLineManagementException(
                "Invoice was not found.");
    }

    private async Task<InvoiceLine> GetLineAsync(
        Guid invoiceLineId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.InvoiceLines
            .SingleOrDefaultAsync(
                x =>
                    x.Id == invoiceLineId &&
                    !x.IsDeleted,
                cancellationToken)
            ?? throw new InvoiceLineManagementException(
                "Invoice line was not found.");
    }

    private static void EnsureInvoiceEditable(
        Invoice invoice)
    {
        if (invoice.Status is not InvoiceStatus.Draft)
        {
            throw new InvoiceLineManagementException(
                $"Invoice lines cannot be modified when invoice status is {invoice.Status}.");
        }
    }

    private async Task EnsureCostCodeAsync(
        Guid companyId,
        Guid? costCodeId,
        CancellationToken cancellationToken)
    {
        if (!costCodeId.HasValue)
        {
            return;
        }

        var exists = await _dbContext.CostCodes
            .AnyAsync(
                x =>
                    x.Id == costCodeId.Value &&
                    x.CompanyId == companyId &&
                    !x.IsDeleted,
                cancellationToken);

        if (!exists)
        {
            throw new InvoiceLineManagementException(
                "Cost code was not found or does not belong to the current company.");
        }
    }

    private static void ValidateLine(
        string? description,
        decimal quantity,
        decimal unitPrice,
        decimal taxAmount)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new InvoiceLineManagementException(
                "Invoice line description is required.");
        }

        if (quantity <= 0)
        {
            throw new InvoiceLineManagementException(
                "Invoice line quantity must be greater than zero.");
        }

        if (unitPrice < 0)
        {
            throw new InvoiceLineManagementException(
                "Invoice line unit price cannot be negative.");
        }

        if (taxAmount < 0)
        {
            throw new InvoiceLineManagementException(
                "Invoice line tax amount cannot be negative.");
        }
    }

    private async Task RecalculateInvoiceTotalsAsync(
        Invoice invoice,
        CancellationToken cancellationToken)
    {
        var totals = await _dbContext.InvoiceLines
            .Where(
                x =>
                    x.InvoiceId == invoice.Id &&
                    !x.IsDeleted)
            .GroupBy(x => x.InvoiceId)
            .Select(
                g => new
                {
                    Subtotal = g.Sum(
                        x => x.Quantity * x.UnitPrice),
                    TaxAmount = g.Sum(
                        x => x.TaxAmount),
                    TotalAmount = g.Sum(
                        x => x.LineTotal)
                })
            .SingleOrDefaultAsync(
                cancellationToken);

        invoice.Subtotal =
            totals?.Subtotal ?? 0m;
        invoice.TaxAmount =
            totals?.TaxAmount ?? 0m;
        invoice.TotalAmount =
            totals?.TotalAmount ?? 0m;

        await _dbContext.SaveChangesAsync(
            cancellationToken);
    }

    private static string CleanRequired(
        string? value,
        string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvoiceLineManagementException(
                $"{fieldName} is required.");
        }

        return value.Trim();
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

    private static InvoiceLineResponse Map(
        InvoiceLine line)
    {
        return new InvoiceLineResponse(
            line.Id,
            line.CompanyId,
            line.InvoiceId,
            line.CostCodeId,
            line.Description,
            line.Quantity,
            line.UnitOfMeasure,
            line.UnitPrice,
            line.TaxAmount,
            line.LineTotal,
            line.CreatedAtUtc,
            line.UpdatedAtUtc);
    }
}
