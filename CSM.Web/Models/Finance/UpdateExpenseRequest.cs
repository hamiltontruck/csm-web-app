public sealed record UpdateExpenseRequest(
    Guid? ConstructionSiteId,
    Guid? ProjectId,
    Guid CostCodeId,
    Guid? VendorId,
    DateOnly ExpenseDate,
    string Description,
    decimal Amount,
    decimal TaxAmount,
    string CurrencyCode,
    decimal ExchangeRate,
    string? ReferenceNumber,
    string? ReceiptDocumentUrl);