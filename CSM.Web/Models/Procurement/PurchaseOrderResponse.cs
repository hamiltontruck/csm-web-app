namespace CSM.Web.Models.Procurement;

public class PurchaseOrderResponse
{
    public Guid Id { get; set; }

    public Guid ConstructionSiteId { get; set; }

    public string SiteName { get; set; } = string.Empty;

    public string PurchaseOrderNumber { get; set; } = string.Empty;

    public DateOnly OrderDate { get; set; }

    public string VendorName { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public decimal TotalAmount { get; set; }

    public string? Notes { get; set; }

    public List<PurchaseOrderLineResponse> Lines { get; set; } = new();
}

public class PurchaseOrderLineResponse
{
    public Guid Id { get; set; }

    public Guid MaterialId { get; set; }

    public string MaterialName { get; set; } = string.Empty;

    public string UnitOfMeasure { get; set; } = string.Empty;

    public decimal OrderedQuantity { get; set; }


    public decimal ReceivedQuantity { get; set; }



    public decimal UnitPrice { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal LineTotal { get; set; }
}