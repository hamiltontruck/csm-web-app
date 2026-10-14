namespace CSM.Web.Models.Procurement;

public class CreatePurchaseOrderRequest
{
    public Guid ConstructionSiteId { get; set; }

    public Guid VendorId { get; set; }

    public string PurchaseOrderNumber { get; set; } = string.Empty;

    public DateOnly OrderDate { get; set; }

    public string? Remarks { get; set; }

    public List<PurchaseOrderLineRequest> Lines { get; set; } = new();
}

public class PurchaseOrderLineRequest
{
    public Guid MaterialId { get; set; }

    public decimal OrderedQuantity { get; set; }

    public decimal UnitPrice { get; set; }

    public string? Remarks { get; set; }
}