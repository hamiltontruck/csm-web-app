namespace CSM.Application.Procurement.PurchaseOrders.Dtos;

public sealed class PurchaseOrderLineResponse
{
    public Guid Id { get; set; }

    public Guid MaterialId { get; set; }

    public string MaterialCode { get; set; } = string.Empty;

    public string MaterialName { get; set; } = string.Empty;

    public string UnitOfMeasure { get; set; } = string.Empty;

    public Guid? CostCodeId { get; set; }

    public string? CostCode { get; set; }

    public decimal OrderedQuantity { get; set; }

    public decimal ReceivedQuantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal LineTotal { get; set; }
}
