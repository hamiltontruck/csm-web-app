using CSM.Domain.Enums;

namespace CSM.Application.Procurement.PurchaseOrders.Dtos;

public sealed class PurchaseOrderResponse
{
    public Guid Id { get; set; }

    public Guid CompanyId { get; set; }

    public Guid ConstructionSiteId { get; set; }

    public string SiteName { get; set; } = string.Empty;

    public Guid? ProjectId { get; set; }

    public string? ProjectName { get; set; }

    public Guid VendorId { get; set; }

    public string VendorCode { get; set; } = string.Empty;

    public string VendorName { get; set; } = string.Empty;

    public Guid? MaterialRequestId { get; set; }

    public string? MaterialRequestNumber { get; set; }

    public string PurchaseOrderNumber { get; set; } = string.Empty;

    public DateOnly OrderDate { get; set; }

    public DateOnly? ExpectedDeliveryDate { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    public decimal ExchangeRate { get; set; }

    public decimal Subtotal { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public PurchaseOrderStatus Status { get; set; }

    public string? DeliveryAddress { get; set; }

    public string? PaymentTerms { get; set; }

    public string? Notes { get; set; }

    public Guid? ApprovedBy { get; set; }

    public DateTime? ApprovedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public List<PurchaseOrderLineResponse> Lines { get; set; } = [];
}
