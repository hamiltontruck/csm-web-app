namespace CSM.Application.Finance.InvoiceLines.Dtos;

public sealed record UpdateInvoiceLineRequest(
    Guid? CostCodeId,
    string Description,
    decimal Quantity,
    string UnitOfMeasure,
    decimal UnitPrice,
    decimal TaxAmount);