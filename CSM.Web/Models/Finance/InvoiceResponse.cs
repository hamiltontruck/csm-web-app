namespace CSM.Web.Models.Finance;

public class InvoiceResponse
{
    public Guid Id { get; set; }

    public Guid? ProjectId { get; set; }

    public Guid? VendorId { get; set; }

    public string InvoiceNumber { get; set; } = string.Empty;

    public DateTime InvoiceDate { get; set; }

    public DateTime? DueDate { get; set; }

    public string InvoiceType { get; set; } = string.Empty;

    public string VendorName { get; set; } = string.Empty;

    public string CurrencyCode { get; set; } = "ETB";

    public decimal TotalAmount { get; set; }

    public decimal PaidAmount { get; set; }

    public string Status { get; set; } = string.Empty;

public List<PaymentResponse> Payments { get; set; } = new();
}