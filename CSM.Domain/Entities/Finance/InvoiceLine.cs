using CSM.Domain.Common;

namespace CSM.Domain.Entities.Finance;

public class InvoiceLine : TenantEntity
{
    public Guid InvoiceId { get; set; }

    public Guid? CostCodeId { get; set; }

    public string Description { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public string UnitOfMeasure { get; set; } = "Pcs";

    public decimal UnitPrice { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal LineTotal { get; set; }

    public Invoice Invoice { get; set; } = null!;

    public CostCode? CostCode { get; set; }


}