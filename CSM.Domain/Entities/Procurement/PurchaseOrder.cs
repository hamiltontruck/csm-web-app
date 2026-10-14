using CSM.Domain.Common;
using CSM.Domain.Entities.Finance;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Entities.Projects;
using CSM.Domain.Enums;

namespace CSM.Domain.Entities.Procurement;

public class PurchaseOrder : TenantEntity
{
    public Guid ConstructionSiteId { get; set; }

    public Guid? ProjectId { get; set; }

    public Guid VendorId { get; set; }

    public Guid? MaterialRequestId { get; set; }

    public string PurchaseOrderNumber { get; set; } = string.Empty;

    public DateOnly OrderDate { get; set; }

    public DateOnly? ExpectedDeliveryDate { get; set; }

    public string CurrencyCode { get; set; } = "ETB";

    public decimal ExchangeRate { get; set; } = 1;

    public decimal Subtotal { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public PurchaseOrderStatus Status { get; set; }
        = PurchaseOrderStatus.Draft;

    public string? DeliveryAddress { get; set; }

    public string? PaymentTerms { get; set; }

    public string? Notes { get; set; }

    public Guid? ApprovedBy { get; set; }

    public DateTime? ApprovedAtUtc { get; set; }

    public ConstructionSite ConstructionSite { get; set; } = null!;

    public Project? Project { get; set; }

    public Vendor Vendor { get; set; } = null!;

    public MaterialRequest? MaterialRequest { get; set; }

    public ICollection<PurchaseOrderLine> Lines { get; set; }
        = new List<PurchaseOrderLine>();

    public ICollection<GoodsReceipt> GoodsReceipts { get; set; }
        = new List<GoodsReceipt>();
}