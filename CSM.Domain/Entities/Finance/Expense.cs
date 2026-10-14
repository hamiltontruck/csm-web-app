using CSM.Domain.Common;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Entities.Projects;
using CSM.Domain.Enums;

namespace CSM.Domain.Entities.Finance;

public class Expense : TenantEntity
{
    public Guid? ConstructionSiteId { get; set; }

    public Guid? ProjectId { get; set; }

    public Guid CostCodeId { get; set; }

    public Guid? VendorId { get; set; }

    public string ExpenseNumber { get; set; } = string.Empty;

    public DateOnly ExpenseDate { get; set; }

    public string Description { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public decimal TaxAmount { get; set; }

    public string CurrencyCode { get; set; } = "ETB";

    public decimal ExchangeRate { get; set; } = 1;

    public string? ReferenceNumber { get; set; }

    public string? ReceiptDocumentUrl { get; set; }

    public ExpenseStatus Status { get; set; } = ExpenseStatus.Draft;

    public Guid? ApprovedBy { get; set; }

    public DateTime? ApprovedAtUtc { get; set; }

    public ConstructionSite? ConstructionSite { get; set; }

    public Project? Project { get; set; }

    public CostCode CostCode { get; set; } = null!;

    public Vendor? Vendor { get; set; }
}