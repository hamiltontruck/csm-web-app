namespace CSM.Web.Models.Finance;

public class CreatePaymentRequest
{
    public Guid? ConstructionSiteId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? InvoiceId { get; set; }
    public Guid? VendorId { get; set; }
    public Guid? PayrollRecordId { get; set; }

    public string PaymentNumber { get; set; } = "";

    public DateOnly PaymentDate { get; set; } =
        DateOnly.FromDateTime(DateTime.Today);

    // 1 = Incoming, 2 = Outgoing
    public int Direction { get; set; }

    public decimal Amount { get; set; }

    public string CurrencyCode { get; set; } = "ETB";

    public decimal ExchangeRate { get; set; } = 1;

    public string? PaymentMethod { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
}