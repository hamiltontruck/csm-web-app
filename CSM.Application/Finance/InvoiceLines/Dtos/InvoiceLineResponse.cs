namespace CSM.Application.Finance.InvoiceLines.Dtos;

public sealed record InvoiceLineResponse(
    Guid Id,
    Guid CompanyId,
    Guid InvoiceId,
    Guid? CostCodeId,
    string Description,
    decimal Quantity,
    string UnitOfMeasure,
    decimal UnitPrice,
    decimal TaxAmount,
    decimal LineTotal,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);
