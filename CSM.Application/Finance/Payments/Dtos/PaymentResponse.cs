using CSM.Domain.Enums;

namespace CSM.Application.Finance.Payments.Dtos;

public sealed record PaymentResponse(
    Guid Id,
    Guid CompanyId,
    Guid? ConstructionSiteId,
    Guid? ProjectId,
    Guid? InvoiceId,
    string? InvoiceNumber,
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
    string? Notes,
    PaymentStatus Status,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);