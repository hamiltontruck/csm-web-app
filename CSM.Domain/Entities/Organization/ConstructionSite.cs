using CSM.Domain.Common;
using CSM.Domain.Entities.Projects;
using CSM.Domain.Enums;
using CSM.Domain.Entities.HRM;
using CSM.Domain.Entities.Finance;
using CSM.Domain.Entities.Procurement;
using CSM.Domain.Entities.Compliance;
using ProjectEquipmentAssignment = CSM.Domain.Entities.Projects.EquipmentAssignment;

namespace CSM.Domain.Entities.Organization;

public class ConstructionSite : TenantEntity
{
    public string SiteCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public Guid? BusinessUnitId { get; set; }

    public string? Address { get; set; }

    public string? City { get; set; }

    public string? Region { get; set; }

    public string? Country { get; set; }

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public decimal? GeofenceRadiusMeters { get; set; }

    public DateOnly? PlannedStartDate { get; set; }

    public DateOnly? PlannedEndDate { get; set; }

    public DateOnly? ActualStartDate { get; set; }

    public DateOnly? ActualEndDate { get; set; }

    public SiteStatus Status { get; set; } = SiteStatus.Planning;

    public bool IsActive { get; set; } = true;

    public Company Company { get; set; } = null!;

    public BusinessUnit? BusinessUnit { get; set; }

    public ICollection<SafetyIncident> SafetyIncidents { get; set; }
    = new List<SafetyIncident>();

public ICollection<Permit> Permits { get; set; }
    = new List<Permit>();

public ICollection<Inspection> Inspections { get; set; }
    = new List<Inspection>();

    public ICollection<StockItem> StockItems { get; set; }
    = new List<StockItem>();

public ICollection<MaterialRequest> MaterialRequests { get; set; }
    = new List<MaterialRequest>();

public ICollection<PurchaseOrder> PurchaseOrders { get; set; }
    = new List<PurchaseOrder>();

public ICollection<GoodsReceipt> GoodsReceipts { get; set; }
    = new List<GoodsReceipt>();

public ICollection<ProjectEquipmentAssignment> EquipmentAssignments { get; set; }
    = new List<ProjectEquipmentAssignment>();

public ICollection<EquipmentDowntime> EquipmentDowntimeRecords { get; set; }
    = new List<EquipmentDowntime>();

    public ICollection<SiteAssignment> SiteAssignments { get; set; }
    = new List<SiteAssignment>();

    public ICollection<Attendance> Attendances { get; set; }
    = new List<Attendance>();

    public ICollection<Project> Projects { get; set; }
        = new List<Project>();
    public ICollection<Budget> Budgets { get; set; }
    = new List<Budget>();

public ICollection<Expense> Expenses { get; set; }
    = new List<Expense>();

public ICollection<Invoice> Invoices { get; set; }
    = new List<Invoice>();

public ICollection<CostCode> CostCodes { get; set; }
    = new List<CostCode>();
}