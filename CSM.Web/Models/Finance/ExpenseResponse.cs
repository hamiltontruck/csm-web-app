namespace CSM.Web.Models.Finance;

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
    string Status,
    Guid? ApprovedBy,
    string? ApprovedByName,
    DateTime? ApprovedAtUtc,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);