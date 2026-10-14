namespace CSM.Web.Models.Finance;

public class InvoiceLineResponse
{
    public Guid Id { get; set; }

    public Guid InvoiceId { get; set; }

    public string Description { get; set; } = "";

    public decimal Quantity { get; set; }

    public string UnitOfMeasure { get; set; } = "";

    public decimal UnitPrice { get; set; }

    public decimal LineTotal { get; set; }
}