namespace CSM.Web.Models.Finance;

public class UpdateInvoiceRequest
{
    public string InvoiceNumber { get; set; } = "";

    public Guid? ConstructionSiteId { get; set; }

    public Guid? ProjectId { get; set; }

    public Guid? VendorId { get; set; }

    // 1 = Client Invoice, 2 = Vendor Bill
    public int Type { get; set; }

    public DateOnly InvoiceDate { get; set; }

    public DateOnly? DueDate { get; set; }

    public string CurrencyCode { get; set; } = "ETB";

    public decimal ExchangeRate { get; set; } = 1m;

    public decimal Subtotal { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal TotalAmount { get; set; }
}