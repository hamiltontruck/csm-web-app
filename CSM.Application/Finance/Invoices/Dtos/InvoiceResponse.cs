using CSM.Application.Finance.Payments.Dtos;
using CSM.Domain.Enums;

namespace CSM.Application.Finance.Invoices.Dtos;

public sealed record InvoiceResponse(
    Guid Id,
    Guid CompanyId,
    Guid? ConstructionSiteId,
    Guid? ProjectId,
    Guid? VendorId,
    string InvoiceNumber,
    InvoiceType Type,
    DateOnly InvoiceDate,
    DateOnly? DueDate,
    decimal Subtotal,
    decimal TaxAmount,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal OutstandingAmount,
    string CurrencyCode,
    decimal ExchangeRate,
    InvoiceStatus Status,
    string? Description,
    string? ExternalReference,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    IReadOnlyCollection<PaymentResponse> Payments);