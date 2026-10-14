using CSM.Domain.Enums;

namespace CSM.Application.Finance.Invoices.Dtos;

public sealed record UpdateInvoiceRequest(
    Guid? ConstructionSiteId,
    Guid? ProjectId,
    Guid? VendorId,
    InvoiceType Type,
    DateOnly InvoiceDate,
    DateOnly? DueDate,
    decimal Subtotal,
    decimal TaxAmount,
    decimal TotalAmount,
    string CurrencyCode,
    decimal ExchangeRate,
    string? Description,
    string? ExternalReference);

