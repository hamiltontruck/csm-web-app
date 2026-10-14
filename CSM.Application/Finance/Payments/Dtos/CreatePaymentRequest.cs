using CSM.Domain.Enums;

namespace CSM.Application.Finance.Payments.Dtos;

public sealed record CreatePaymentRequest(
    Guid? ConstructionSiteId,
    Guid? ProjectId,
    Guid? InvoiceId,
    Guid? VendorId,
    Guid? PayrollRecordId,
    string PaymentNumber,
    DateOnly PaymentDate,
    PaymentDirection Direction,
    decimal Amount,
    string CurrencyCode,
    decimal ExchangeRate,
    string? PaymentMethod,
    string? ReferenceNumber,
    string? Notes);