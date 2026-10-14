using CSM.Domain.Common;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Entities.Projects;
using CSM.Domain.Enums;
using CSM.Domain.Entities.HRM;

namespace CSM.Domain.Entities.Finance;

public class Payment : TenantEntity
{
    public Guid? ConstructionSiteId { get; set; }

    public Guid? ProjectId { get; set; }

    public Guid? InvoiceId { get; set; }

    public Guid? VendorId { get; set; }

    public Guid? PayrollRecordId { get; set; }

    public string PaymentNumber { get; set; } = string.Empty;

    public DateOnly PaymentDate { get; set; }

    public PaymentDirection Direction { get; set; }

    public decimal Amount { get; set; }

    public string CurrencyCode { get; set; } = "ETB";

    public decimal ExchangeRate { get; set; } = 1;

    public string? PaymentMethod { get; set; }

    public string? ReferenceNumber { get; set; }

    public string? Notes { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    public ConstructionSite ConstructionSite { get; set; } = null!;

    public Project? Project { get; set; }

    public Invoice? Invoice { get; set; }

    public Vendor? Vendor { get; set; }

    public PayrollRecord? PayrollRecord { get; set; }
}