namespace CSM.Application.Procurement.GoodsReceipts.Dtos;

public sealed class CreateGoodsReceiptLineRequest
{
    public Guid PurchaseOrderLineId { get; set; }

    public decimal ReceivedQuantity { get; set; }

    public decimal AcceptedQuantity { get; set; }

    public decimal RejectedQuantity { get; set; }

    public string? RejectionReason { get; set; }

    public string? Remarks { get; set; }
}
