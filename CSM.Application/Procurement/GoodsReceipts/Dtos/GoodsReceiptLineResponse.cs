namespace CSM.Application.Procurement.GoodsReceipts.Dtos;

public sealed class GoodsReceiptLineResponse
{
    public Guid Id { get; set; }

    public Guid PurchaseOrderLineId { get; set; }

    public Guid MaterialId { get; set; }

    public string MaterialCode { get; set; } = string.Empty;

    public string MaterialName { get; set; } = string.Empty;

    public decimal ReceivedQuantity { get; set; }

    public decimal OrderedQuantity { get; set; }
    public decimal RemainingQuantity { get; set; }

    public decimal AcceptedQuantity { get; set; }

    public decimal RejectedQuantity { get; set; }

    public decimal? StockBalanceBefore { get; set; }

    public decimal? StockBalanceAfter { get; set; }

    public string? RejectionReason { get; set; }

    public string? Remarks { get; set; }
}