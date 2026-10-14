using CSM.Domain.Common;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Entities.Projects;
using CSM.Domain.Enums;

namespace CSM.Domain.Entities.Finance;

public class Invoice : TenantEntity
{
    public Guid? ConstructionSiteId { get; set; }

    public ConstructionSite? ConstructionSite { get; set; }

    public Guid? ProjectId { get; set; }

    public Guid? VendorId { get; set; }

    public string InvoiceNumber { get; set; } = string.Empty;

    public InvoiceType Type { get; set; }

    public DateOnly InvoiceDate { get; set; }

    public DateOnly? DueDate { get; set; }

    public decimal Subtotal { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public decimal PaidAmount { get; set; } = 0m;

    public string CurrencyCode { get; set; } = "ETB";

    public decimal ExchangeRate { get; set; } = 1;

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;

    public string? Description { get; set; }

    public string? ExternalReference { get; set; }



    public Project? Project { get; set; }

    public Vendor? Vendor { get; set; }

    public ICollection<InvoiceLine> Lines { get; set; }
        = new List<InvoiceLine>();

    public ICollection<Payment> Payments { get; set; }
        = new List<Payment>();
}