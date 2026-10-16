using CSM.Domain.Entities.Compliance;
using CSM.Domain.Entities.Finance;
using CSM.Domain.Entities.HRM;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Entities.Procurement;
using CSM.Domain.Entities.Projects;
using Microsoft.EntityFrameworkCore;
using CSM.Domain.Common;
using CSM.Domain.Entities.Equipment;

using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Notifications;
using ProjectEquipment = CSM.Domain.Entities.Projects.Equipment;
using ProjectEquipmentAssignment = CSM.Domain.Entities.Projects.EquipmentAssignment;
using CSM.Domain.Entities.Audit;
using CSM.Infrastructure.Services;



namespace CSM.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // Organization
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<BusinessUnit> BusinessUnits => Set<BusinessUnit>();
    public DbSet<ConstructionSite> ConstructionSites => Set<ConstructionSite>();
    public DbSet<UserSiteAccess> UserSiteAccesses => Set<UserSiteAccess>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserAudit> UserAudits => Set<UserAudit>();

    // Projects
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectPhase> ProjectPhases => Set<ProjectPhase>();
    public DbSet<Milestone> Milestones => Set<Milestone>();
    public DbSet<WorkTask> WorkTasks => Set<WorkTask>();
    public DbSet<WorkTaskDependency> WorkTaskDependencies => Set<WorkTaskDependency>();
    public DbSet<DailyProgressPhoto> DailyProgressPhotos => Set<DailyProgressPhoto>();

    public DbSet<DailyProgressLog> DailyProgressLogs => Set<DailyProgressLog>();
    public DbSet<DailyProgressTaskEntry> DailyProgressTaskEntries => Set<DailyProgressTaskEntry>();
    public DbSet<DailyProgressEquipmentEntry> DailyProgressEquipmentEntries => Set<DailyProgressEquipmentEntry>();
    public DbSet<DailySiteDiary> DailySiteDiaries => Set<DailySiteDiary>();
    public DbSet<ProjectDelay> ProjectDelays => Set<ProjectDelay>();



        public DbSet<WbsTask> WbsTasks => Set<WbsTask>();
        public DbSet<TaskResourceAssignment> TaskResourceAssignments => Set<TaskResourceAssignment>();



    // HRM
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Contractor> Contractors => Set<Contractor>();
    public DbSet<SiteAssignment> SiteAssignments => Set<SiteAssignment>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<Attendance> Attendances => Set<Attendance>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<Timesheet> Timesheets => Set<Timesheet>();
    public DbSet<PayrollRecord> PayrollRecords => Set<PayrollRecord>();
    public DbSet<PayrollAdjustment> PayrollAdjustments => Set<PayrollAdjustment>();
    public DbSet<SalaryStructure> SalaryStructures => Set<SalaryStructure>();

    // Finance
    public DbSet<CostCode> CostCodes => Set<CostCode>();
    public DbSet<Budget> Budgets => Set<Budget>();
    public DbSet<BudgetLine> BudgetLines => Set<BudgetLine>();
    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PettyCashAccount> PettyCashAccounts => Set<PettyCashAccount>();
    public DbSet<PettyCashTransaction> PettyCashTransactions => Set<PettyCashTransaction>();


    // Procurement
    public DbSet<Material> Materials => Set<Material>();
    public DbSet<RebarSpec> RebarSpecs => Set<RebarSpec>();
    public DbSet<StockItem> StockItems => Set<StockItem>();
    public DbSet<DailyMaterialUsage> DailyMaterialUsages =>
        Set<DailyMaterialUsage>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<MaterialRequest> MaterialRequests => Set<MaterialRequest>();
    public DbSet<MaterialRequestLine> MaterialRequestLines => Set<MaterialRequestLine>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderLine> PurchaseOrderLines => Set<PurchaseOrderLine>();
    public DbSet<GoodsReceipt> GoodsReceipts => Set<GoodsReceipt>();
    public DbSet<GoodsReceiptLine> GoodsReceiptLines => Set<GoodsReceiptLine>();
    // Procurement (existing module)
    // Procurement Equipment
    public DbSet<CSM.Domain.Entities.Procurement.Equipment> Equipment
        => Set<CSM.Domain.Entities.Procurement.Equipment>();

    public DbSet<CSM.Domain.Entities.Procurement.EquipmentAssignment> EquipmentAssignments
        => Set<CSM.Domain.Entities.Procurement.EquipmentAssignment>();
    public DbSet<EquipmentMaintenance> EquipmentMaintenanceRecords => Set<EquipmentMaintenance>();
    public DbSet<EquipmentDowntime> EquipmentDowntimeRecords => Set<EquipmentDowntime>();
    public DbSet<EquipmentFuelLog> EquipmentFuelLogs => Set<EquipmentFuelLog>();
    public DbSet<EquipmentHourLog> EquipmentHourLogs => Set<EquipmentHourLog>();
    public DbSet<EquipmentTimesheet> EquipmentTimesheets => Set<EquipmentTimesheet>();

    // Project Tracking (new module)
    public DbSet<ProjectEquipment> ProjectEquipment => Set<ProjectEquipment>();
    public DbSet<ProjectEquipmentAssignment> ProjectEquipmentAssignments => Set<ProjectEquipmentAssignment>();
    public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();

    // Compliance
    public DbSet<Document> Documents => Set<Document>();
public DbSet<DocumentCategory> DocumentCategories => Set<DocumentCategory>();
    public DbSet<SafetyIncident> SafetyIncidents => Set<SafetyIncident>();
    public DbSet<Permit> Permits => Set<Permit>();
    public DbSet<Inspection> Inspections => Set<Inspection>();
    public DbSet<SafetyObservation> SafetyObservations => Set<SafetyObservation>();
    public DbSet<InspectionChecklistItem> InspectionChecklistItems =>
        Set<InspectionChecklistItem>();
    public DbSet<NonConformanceReport> NonConformanceReports => Set<NonConformanceReport>();
    public DbSet<QualityChecklist> QualityChecklists => Set<QualityChecklist>();
    public DbSet<QualityChecklistItem> QualityChecklistItems => Set<QualityChecklistItem>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // Identity
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
public DbSet<Notification> Notifications => Set<Notification>();
public DbSet<RoleGranularPermission> RoleGranularPermissions { get; set; }
    = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(ApplicationDbContext).Assembly);

       modelBuilder.Entity<MaterialRequest>(entity =>
       {
           entity.HasOne(x => x.RequestedByUser)
               .WithMany()
               .HasForeignKey(x => x.RequestedBy)
               .OnDelete(DeleteBehavior.Restrict);

           entity.HasOne(x => x.ApprovedByUser)
               .WithMany()
               .HasForeignKey(x => x.ApprovedBy)
               .OnDelete(DeleteBehavior.Restrict);
       });

        modelBuilder.Entity<NonConformanceReport>(entity =>
            {
                entity.HasOne(x => x.ConstructionSite)
                    .WithMany()
                    .HasForeignKey(x => x.ConstructionSiteId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(x => x.Project)
                    .WithMany()
                    .HasForeignKey(x => x.ProjectId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(x => x.RaisedBy)
                    .WithMany()
                    .HasForeignKey(x => x.RaisedById)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(x => x.AssignedToEmployee)
                    .WithMany()
                    .HasForeignKey(x => x.AssignedToEmployeeId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(x => x.NcrNumber)
                    .HasMaxLength(50);

                entity.Property(x => x.Title)
                    .HasMaxLength(200);

                entity.Property(x => x.Location)
                    .HasMaxLength(200);
            });
            modelBuilder.Entity<QualityChecklist>(entity =>
            {
                entity.HasOne(x => x.ConstructionSite)
                    .WithMany()
                    .HasForeignKey(x => x.ConstructionSiteId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(x => x.Name).HasMaxLength(200);
                entity.Property(x => x.Category).HasMaxLength(100);

                entity.HasMany(x => x.Items)
                    .WithOne(x => x.QualityChecklist)
                    .HasForeignKey(x => x.QualityChecklistId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<QualityChecklistItem>(entity =>
            {
                entity.Property(x => x.Requirement)
                    .HasMaxLength(500);
            });

            modelBuilder.Entity<EquipmentFuelLog>(builder =>
            {
                builder.HasKey(x => x.Id);

                builder.Property(x => x.FuelType)
                    .HasMaxLength(30);

                builder.Property(x => x.SupplierName)
                    .HasMaxLength(200);

                builder.Property(x => x.InvoiceNumber)
                    .HasMaxLength(100);

                builder.Property(x => x.Liters)
                    .HasPrecision(18, 2);

                builder.Property(x => x.UnitPrice)
                    .HasPrecision(18, 2);

                builder.Property(x => x.TotalCost)
                    .HasPrecision(18, 2);

                builder.Property(x => x.MeterReading)
                    .HasPrecision(18, 2);

                builder.HasOne(x => x.Equipment)
                    .WithMany(x => x.FuelLogs)
                    .HasForeignKey(x => x.EquipmentId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<EquipmentHourLog>(builder =>
            {
                builder.HasKey(x => x.Id);

                builder.Property(x => x.PreviousMeterReading);
                builder.Property(x => x.CurrentMeterReading);
                builder.Property(x => x.HoursWorked).HasPrecision(18,2);

                builder.Property(x => x.OperatorName).HasMaxLength(200);
                builder.Property(x => x.ProjectName);
                builder.Property(x => x.Activity).HasMaxLength(300);

                builder.HasOne(x => x.Equipment)
                    .WithMany(x => x.HourLogs)
                    .HasForeignKey(x => x.EquipmentId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<SalaryStructure>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.Property(x => x.BasicSalary).HasPrecision(18, 2);
                entity.Property(x => x.HousingAllowance).HasPrecision(18, 2);
                entity.Property(x => x.TransportAllowance).HasPrecision(18, 2);
                entity.Property(x => x.PositionAllowance).HasPrecision(18, 2);

                entity.Property(x => x.OvertimeMultiplier).HasPrecision(5, 2);
                entity.Property(x => x.PensionPercentage).HasPrecision(5, 2);

                entity.HasOne(x => x.Employee)
                    .WithMany()
                    .HasForeignKey(x => x.EmployeeId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<SafetyObservation>()
                .Property(x => x.ObservationDate)
                .HasColumnType("date");
            modelBuilder.Entity<NonConformanceReport>()
                .Property(x => x.ReportedDate)
                .HasColumnType("date");
            modelBuilder.Entity<NonConformanceReport>()
                .Property(x => x.DueDate)
                .HasColumnType("date");
            modelBuilder.Entity<NonConformanceReport>()
                .Property(x => x.ClosedDate)
                .HasColumnType("date");
            modelBuilder.Entity<ProjectDelay>()
                .Property(x => x.DelayDate)
                .HasColumnType("date");
            modelBuilder.Entity<ProjectDelay>()
                .Property(x => x.ResolvedDate)
                .HasColumnType("date");
    }


    public override int SaveChanges()
{
    PrepareForSave();
    return base.SaveChanges();
}

public override Task<int> SaveChangesAsync(
    CancellationToken cancellationToken = default)
{
    PrepareForSave();

    return base.SaveChangesAsync(cancellationToken);
}

private void PrepareForSave()
{
    ApplyAuditInformation();
    NormalizePostgresTimestamps();
}

private void NormalizePostgresTimestamps()
{
    foreach (var entry in ChangeTracker.Entries()
                 .Where(x => x.State is EntityState.Added or EntityState.Modified))
    {
        foreach (var property in entry.Properties)
        {
            var clrType = Nullable.GetUnderlyingType(property.Metadata.ClrType)
                ?? property.Metadata.ClrType;
            if (clrType != typeof(DateTime) ||
                property.Metadata.GetColumnType() == "date" ||
                property.CurrentValue is not DateTime value)
            {
                continue;
            }

            property.CurrentValue = PostgresDateTimeNormalizer.Normalize(value);
        }
    }
}

private void ApplyAuditInformation()
{
    var utcNow = DateTime.UtcNow;

    foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
    {
        switch (entry.State)
        {
            case EntityState.Added:
                entry.Entity.CreatedAtUtc = utcNow;
                entry.Entity.IsDeleted = false;
                break;

            case EntityState.Modified:
                entry.Entity.UpdatedAtUtc = utcNow;

                // Created audit fields must never change during an update.
                entry.Property(x => x.CreatedAtUtc).IsModified = false;
                entry.Property(x => x.CreatedBy).IsModified = false;
                break;

            case EntityState.Deleted:
                // Convert physical deletion into soft deletion.
                entry.State = EntityState.Modified;

                entry.Entity.IsDeleted = true;
                entry.Entity.DeletedAtUtc = utcNow;
                entry.Entity.UpdatedAtUtc = utcNow;

                entry.Property(x => x.CreatedAtUtc).IsModified = false;
                entry.Property(x => x.CreatedBy).IsModified = false;
                break;
        }
    }
}
}








