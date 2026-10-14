using System;

namespace CSM.Web.Models.Finance;

public class CreateInvoiceRequest
{
    public Guid? ConstructionSiteId { get; set; }

    public Guid? ProjectId { get; set; }

    public Guid? VendorId { get; set; }

    public string InvoiceNumber { get; set; } = "";

    // 1 = ClientInvoice, 2 = VendorBill
    public int Type { get; set; } = 2;

    public DateOnly InvoiceDate { get; set; }

    public DateOnly? DueDate { get; set; }

    public decimal Subtotal { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public string CurrencyCode { get; set; } = "ETB";

    public decimal ExchangeRate { get; set; } = 1;

    public string? Description { get; set; }

    public string? ExternalReference { get; set; }
}