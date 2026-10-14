namespace CSM.Web.Models.Finance;

public class PaymentResponse
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid? ConstructionSiteId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? InvoiceId { get; set; }
    public string? InvoiceNumber { get; set; }
    public Guid? VendorId { get; set; }
    public Guid? PayrollRecordId { get; set; }

    public string PaymentNumber { get; set; } = "";

    public DateOnly PaymentDate { get; set; }

    // API returns enum names
    public string Direction { get; set; } = "";

    public decimal Amount { get; set; }

    public string CurrencyCode { get; set; } = "";

    public decimal ExchangeRate { get; set; }

    public string? PaymentMethod { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }

    // API returns enum names
    public string Status { get; set; } = "";

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}