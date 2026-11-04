namespace CSM.Web.Models.Finance;

public class InvoiceResponse
{
    public Guid Id { get; set; }

    public Guid? ConstructionSiteId { get; set; }

    public Guid? ProjectId { get; set; }

    public Guid? VendorId { get; set; }

    public string InvoiceNumber { get; set; } = string.Empty;

    public DateTime InvoiceDate { get; set; }

    public DateTime? DueDate { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("type")]
    public string InvoiceType { get; set; } = string.Empty;

    public string VendorName { get; set; } = string.Empty;

    public string CurrencyCode { get; set; } = "ETB";

    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal ExchangeRate { get; set; }
    public string? Description { get; set; }
    public string? ExternalReference { get; set; }

    public decimal TotalAmount { get; set; }

    public decimal PaidAmount { get; set; }

    public string Status { get; set; } = string.Empty;

public List<PaymentResponse> Payments { get; set; } = new();
}