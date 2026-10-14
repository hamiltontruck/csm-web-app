using CSM.Domain.Common;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Enums;
using CSM.Domain.Entities.Finance;
using CSM.Domain.Entities.Procurement;
using CSM.Domain.Entities.Compliance;


namespace CSM.Domain.Entities.Projects;

public class Project : TenantEntity
{
    public Guid ConstructionSiteId { get; set; }

    public string ProjectCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateOnly? PlannedStartDate { get; set; }

    public DateOnly? PlannedEndDate { get; set; }

    public DateOnly? ActualStartDate { get; set; }

    public DateOnly? ActualEndDate { get; set; }

    public decimal ContractValue { get; set; }

    public decimal ProgressPercentage { get; set; }

    public ProjectStatus Status { get; set; } = ProjectStatus.Draft;

    public ConstructionSite ConstructionSite { get; set; } = null!;

    public ICollection<SafetyIncident> SafetyIncidents { get; set; }
    = new List<SafetyIncident>();

public ICollection<Permit> Permits { get; set; }
    = new List<Permit>();

public ICollection<Inspection> Inspections { get; set; }
    = new List<Inspection>();

    public ICollection<MaterialRequest> MaterialRequests { get; set; }
    = new List<MaterialRequest>();

public ICollection<PurchaseOrder> PurchaseOrders { get; set; }
    = new List<PurchaseOrder>();

public ICollection<EquipmentAssignment> EquipmentAssignments { get; set; }
    = new List<EquipmentAssignment>();

    public ICollection<Budget> Budgets { get; set; }
    = new List<Budget>();

public ICollection<Expense> Expenses { get; set; }
    = new List<Expense>();

public ICollection<Invoice> Invoices { get; set; }
    = new List<Invoice>();

    public ICollection<ProjectPhase> Phases { get; set; }
        = new List<ProjectPhase>();

    public ICollection<Milestone> Milestones { get; set; }
        = new List<Milestone>();

    public ICollection<WorkTask> Tasks { get; set; }
        = new List<WorkTask>();

    public ICollection<DailyProgressLog> DailyProgressLogs { get; set; }
        = new List<DailyProgressLog>();
}