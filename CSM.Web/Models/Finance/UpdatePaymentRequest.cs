namespace CSM.Web.Models.Finance;

public class UpdatePaymentRequest
{
    public Guid CompanyId { get; set; }
    public Guid? ConstructionSiteId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? InvoiceId { get; set; }
    public Guid? VendorId { get; set; }
    public Guid? PayrollRecordId { get; set; }

    public string PaymentNumber { get; set; } = string.Empty;
    public DateOnly PaymentDate { get; set; }

    public decimal Amount { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;
    public decimal ExchangeRate { get; set; } = 1;

    public int Direction { get; set; } = 2;

    public string PaymentMethod { get; set; } = "Cash";
    public string ReferenceNumber { get; set; } = string.Empty;
    public string Status { get; set; } = "Recorded";
    public string Notes { get; set; } = string.Empty;
}