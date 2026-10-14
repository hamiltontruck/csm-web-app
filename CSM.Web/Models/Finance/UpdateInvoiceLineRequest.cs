namespace CSM.Web.Models.Finance;

public class UpdateInvoiceLineRequest
{
    public string Description { get; set; } = "";

    public decimal Quantity { get; set; }

    public string UnitOfMeasure { get; set; } = "Pcs";

    public decimal UnitPrice { get; set; }
}