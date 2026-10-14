namespace CSM.Web.Models.Finance;

public class CreateInvoiceLineRequest
{
    public Guid InvoiceId { get; set; }

    public string Description { get; set; } = "";

    public decimal Quantity { get; set; }

    public string UnitOfMeasure { get; set; } = "";

    public decimal UnitPrice { get; set; }
}