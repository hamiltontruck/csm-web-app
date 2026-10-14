using CSM.Domain.Enums;

namespace CSM.Application.Finance.Expenses.Dtos;

public sealed record ExpenseResponse(
    Guid Id,
    Guid CompanyId,
    Guid? ConstructionSiteId,
    Guid? ProjectId,
    Guid CostCodeId,
    Guid? VendorId,
    string ExpenseNumber,
    DateOnly ExpenseDate,
    string Description,
    decimal Amount,
    decimal TaxAmount,
    string CurrencyCode,
    decimal ExchangeRate,
    string? ReferenceNumber,
    string? ReceiptDocumentUrl,
    ExpenseStatus Status,
    Guid? ApprovedBy,
    string? ApprovedByName,
    DateTime? ApprovedAtUtc,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);
