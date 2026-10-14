namespace CSM.Application.Procurement.PurchaseOrders.Dtos;

public sealed class PurchaseOrderLineRequest
{
    public Guid MaterialId { get; set; }

    public Guid? CostCodeId { get; set; }

    public decimal OrderedQuantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal TaxAmount { get; set; }
}
