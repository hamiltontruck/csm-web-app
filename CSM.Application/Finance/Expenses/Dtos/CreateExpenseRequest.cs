namespace CSM.Application.Finance.Expenses.Dtos;

public sealed record CreateExpenseRequest(
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
    string? ReceiptDocumentUrl);
