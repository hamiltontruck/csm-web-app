namespace CSM.Application.Procurement.PurchaseOrders.Dtos;

public sealed class CreatePurchaseOrderRequest
{
    public Guid ConstructionSiteId { get; set; }

    public Guid? ProjectId { get; set; }

    public Guid VendorId { get; set; }

    public Guid? MaterialRequestId { get; set; }

    public string PurchaseOrderNumber { get; set; } = string.Empty;

    public DateOnly OrderDate { get; set; }

    public DateOnly? ExpectedDeliveryDate { get; set; }

    public string CurrencyCode { get; set; } = "ETB";

    public decimal ExchangeRate { get; set; } = 1m;

    public decimal DiscountAmount { get; set; }

    public string? DeliveryAddress { get; set; }

    public string? PaymentTerms { get; set; }

    public string? Notes { get; set; }

    public List<PurchaseOrderLineRequest> Lines { get; set; } = [];
}
