using CSM.Application.Inventory.StockAdjustments;
using CSM.Application.Procurement.RebarSpecs;
using CSM.Application.Procurement.GoodsReceipts;
using CSM.Application.Inventory.StockReturns;
using CSM.Application.Inventory.StockMovements;
using CSM.Application.Inventory.StockIssues;
using CSM.Application.Inventory.DailyMaterialUsage;
using CSM.Application.Procurement.PurchaseOrders;
using CSM.Application.Procurement.Vendors;
using CSM.Application.Procurement.MaterialRequests;
using CSM.Application.Procurement.Materials;
using CSM.Application.Procurement.Equipment;
using CSM.Application.Procurement.EquipmentAssignments;
using CSM.Application.Procurement.EquipmentMaintenance;
using CSM.Application.Procurement.EquipmentDowntime;
using CSM.Application.HRM.Contractors;
using CSM.Application.Finance.CostCodes;
using CSM.Application.Finance.Budgets;
using CSM.Application.Finance.BudgetLines;
using CSM.Application.Finance.Expenses;
using CSM.Application.Finance.Invoices;
using CSM.Application.Finance.InvoiceLines;
using CSM.Application.Finance.Payments;
using CSM.Application.Reporting;
using CSM.Application.Notifications;
using CSM.Application.Projects.DailySiteDiaries;
using CSM.Application.Projects.DailyProgressPhotos;
using CSM.Application.Projects.Equipment;
using System.Security.Claims;
using System.Text;
using CSM.Application.Common.Security;
using CSM.Application.Compliance.SafetyIncidents;
using CSM.Application.Features.Compliance.SafetyObservations.Services;
using CSM.Infrastructure.Features.Compliance.SafetyObservations;
using CSM.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using CSM.Domain.Enums;
using CSM.Infrastructure.Security;
using CSM.Infrastructure.Features.Compliance.NonConformanceReports;
using CSM.Infrastructure.Features.Compliance.QualityChecklists;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

using CSM.Application.Auth;
using CSM.Application.Users;
using CSM.Infrastructure.Services;
using CSM.Application.Companies;
using CSM.Application.Compliance.DocumentCategories;
using CSM.Application.Compliance.Documents;
using CSM.Application.Compliance.Permits;
using CSM.Application.Compliance.Inspections;
using CSM.Application.Sites;
using CSM.Application.Projects;
using CSM.Application.Projects.Phases;
using CSM.Application.Projects.Milestones;
using CSM.Application.Projects.Tasks;
using CSM.Application.Projects.DailyProgress;
using CSM.Application.HRM.Shifts;
using CSM.Application.HRM.Employees;
using CSM.Application.HRM.SiteAssignments;
using CSM.Application.HRM.Attendances;
using CSM.Application.HRM.Leaves;
using CSM.Application.HRM.Timesheets;
using CSM.Application.HRM.Payroll;
using CSM.Application.Inventory.StockTransfers;
using CSM.Application.Inventory.StockBalances;
using CSM.Application.Finance.PettyCashAccounts;
using CSM.Application.Finance.PettyCashTransactions;
using CSM.Application.CompanySettings;
using CSM.Application.Payroll;
using CSM.Application.Payroll.Calculations;
using CSM.Application.Common.Backup;

using CSM.Application.Common.Audit;
using CSM.Application.Projects.ProjectDelays;
using CSM.Application.Projects.ProjectMilestones;
using CSM.Application.Projects.WbsTasks;
using CSM.Application.Projects.Progress;
using CSM.Application.Projects.ResourceAssignments;
using CSM.Application.Dashboard;
using CSM.Application.Roles;
using CSM.Application.Projects.DailyProgressTaskEntries;

namespace CSM.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' was not found.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                postgresOptions => postgresOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorCodesToAdd: null)));

        services.Configure<JwtSettings>(
            configuration.GetSection(JwtSettings.SectionName));

        var jwtSettings =
            configuration
                .GetSection(JwtSettings.SectionName)
                .Get<JwtSettings>()
            ?? throw new InvalidOperationException(
                "JWT configuration was not found.");

        if (string.IsNullOrWhiteSpace(jwtSettings.SecretKey) ||
            jwtSettings.SecretKey.Length < 32)
        {
            throw new InvalidOperationException(
                "JWT SecretKey must contain at least 32 characters.");
        }

        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ICompanyService, CompanyService>();
        services.AddScoped<ISiteService, SiteService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<
            IDailyProgressLogService,
            DailyProgressLogService>();
        services.AddScoped<IProjectPhaseService, ProjectPhaseService>();
        services.AddScoped<IProjectEquipmentService, ProjectEquipmentService>();
        services.AddScoped<IDailyProgressPhotoService, DailyProgressPhotoService>();
        services.AddScoped<IMilestoneService, MilestoneService>();
        services.AddScoped<IWorkTaskService, WorkTaskService>();
        services.AddScoped<IShiftService, ShiftService>();
        services.AddScoped<IProjectDelayService, ProjectDelayService>();
        services.AddScoped<IDailyProgressTaskEntryService, DailyProgressTaskEntryService>();

        services.AddScoped<
            ISiteAssignmentService,
            SiteAssignmentService>();
        services.AddScoped<
            IAttendanceService,
            AttendanceService>();
        services.AddScoped<
            ILeaveService,
            LeaveService>();
        services.AddScoped<ITimesheetService, TimesheetService>();
        services.AddScoped<IPayrollService, PayrollService>();
        services.AddScoped<IMaterialService, MaterialService>();
        services.AddScoped<IEquipmentService, EquipmentService>();
        services.AddScoped<IEquipmentAssignmentService, EquipmentAssignmentService>();
        services.AddScoped<IEquipmentMaintenanceService, EquipmentMaintenanceService>();
        services.AddScoped<EquipmentTimesheetService>();
        services.AddScoped<
            IEquipmentDowntimeService,
            EquipmentDowntimeService>();
        services.AddScoped<IRebarSpecService, RebarSpecService>();
        services.AddScoped<IVendorService, VendorService>();
services.AddScoped<IDocumentCategoryService, DocumentCategoryService>();
services.AddScoped<IDocumentService, DocumentService>();
services.AddScoped<IDocumentVersionService, DocumentVersionService>();
services.AddScoped<IPermitService, PermitService>();
services.AddScoped<IInspectionService, InspectionService>();
services.AddScoped<ISafetyIncidentService, SafetyIncidentService>();
services.AddScoped<ISafetyObservationService, SafetyObservationService>();
services.AddScoped<NonConformanceReportService>();
services.AddScoped<QualityChecklistService>();
        services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
        services.AddScoped<IMaterialRequestService, MaterialRequestService>();
        services.AddScoped<IGoodsReceiptService, GoodsReceiptService>();
    services.AddScoped<IStockReturnService, StockReturnService>();
    services.AddScoped<IStockMovementService, StockMovementService>();
    services.AddScoped<IStockIssueService, StockIssueService>();
    services.AddScoped<
        ILowStockNotificationService,
        LowStockNotificationService>();
    services.AddScoped<
        IDailyMaterialUsageService,
        DailyMaterialUsageService>();
        services.AddScoped<IContractorService, ContractorService>();
        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<IStockTransferService, StockTransferService>();
        services.AddScoped<IStockAdjustmentService, StockAdjustmentService>();
        services.AddScoped<IStockBalanceService, StockBalanceService>();
        services.AddScoped<ICostCodeService, CostCodeService>();
        services.AddScoped<IBudgetService, BudgetService>();
        services.AddScoped<IBudgetLineService, BudgetLineService>();
        services.AddScoped<IExpenseService, ExpenseService>();
        services.AddScoped<ISalaryStructureService, SalaryStructureService>();
        services.AddScoped<IPayrollCalculationService, PayrollCalculationService>();
        services.AddScoped<IInvoiceService, InvoiceService>();
        services.AddScoped<IInvoiceLineService, InvoiceLineService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IPettyCashAccountService, PettyCashAccountService>();
        services.AddScoped<
            IPettyCashTransactionService,
            PettyCashTransactionService>();
        services.AddScoped<
            CSM.Application.Finance.Summaries.IFinancialSummaryService,
            CSM.Infrastructure.Services.FinancialSummaryService>();
        services.AddScoped<IReportingService, ReportingService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IDailySiteDiaryService, DailySiteDiaryService>();
        services.AddScoped<ICompanySettingsService, CompanySettingsService>();
        services.AddScoped<IBackupService, BackupService>();
        services.AddScoped<IAuditService, AuditService>();
services.AddScoped<IAuditQueryService, AuditQueryService>();
services.AddScoped<IProjectDelayService, ProjectDelayService>();
services.AddScoped<IProjectMilestoneService, ProjectMilestoneService>();
services.AddScoped<IWbsTaskService, WbsTaskService>();
services.AddScoped<IProjectProgressService, ProjectProgressService>();
services.AddScoped<ITaskResourceAssignmentService, TaskResourceAssignmentService>();
services.AddScoped<IExecutiveDashboardService, ExecutiveDashboardService>();
services.AddScoped<IRolePermissionService, RolePermissionService>();
services.AddScoped<
    IAuthorizationHandler,
    PermissionAuthorizationHandler>();
services.AddScoped<IPermissionEvaluator, PermissionEvaluator>();


        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme =
                    JwtBearerDefaults.AuthenticationScheme;

                options.DefaultChallengeScheme =
                    JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;

                options.TokenValidationParameters =
                    new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = jwtSettings.Issuer,

                        ValidateAudience = true,
                        ValidAudience = jwtSettings.Audience,

                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey =
                            new SymmetricSecurityKey(
                                Encoding.UTF8.GetBytes(
                                    jwtSettings.SecretKey)),

                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromSeconds(30),

                        NameClaimType = ClaimTypes.Name,
                        RoleClaimType = ClaimTypes.Role
                    };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(
                AppPolicies.UserManagement,
                policy => policy.RequireRole(
                    AppPolicies.UserManagementRoles));

            options.AddPolicy(
                AppPolicies.SiteManagement,
                policy => policy.RequireRole(
                    AppPolicies.SiteManagementRoles));

            options.AddPolicy(
                AppPolicies.HRManagement,
                policy => policy.RequireRole(
                    AppPolicies.HRManagementRoles));

            options.AddPolicy(
                AppPolicies.FinanceManagement,
                policy => policy.RequireRole(
                    AppPolicies.FinanceManagementRoles));

            options.AddPolicy(
                AppPolicies.ProcurementManagement,
                policy => policy.RequireRole(
                    AppPolicies.ProcurementManagementRoles));

                   options.AddPolicy(
                       AppPolicies.CompanyManagement,
                       policy => policy.RequireRole(
                           AppPolicies.CompanyManagementRoles));
           options.AddPolicy(
               AppPolicies.ProjectManagement,
               policy => policy.RequireRole(
                   AppPolicies.ProjectManagementRoles));

                   options.AddPolicy(
                       AppPolicies.ReportManagement,
                       policy => policy.RequireRole(
                           AppPolicies.ReportManagementRoles));

                     options.AddPolicy(
                         "Users.View",
                         policy =>
                         {
                             policy.RequireAuthenticatedUser();
                             policy.AddRequirements(
                                 new PermissionRequirement(
                                     GranularPermission.Users_View.ToString()));
                         });

                         options.AddPolicy(
                             "Users.Create",
                             policy =>
                             {
                                 policy.RequireAuthenticatedUser();
                                 policy.AddRequirements(
                                     new PermissionRequirement(
                                         GranularPermission.Users_Create.ToString()));
                             });

                             options.AddPolicy(
                                 "Users.Edit",
                                 policy =>
                                 {
                                     policy.RequireAuthenticatedUser();
                                     policy.AddRequirements(
                                         new PermissionRequirement(
                                             GranularPermission.Users_Edit.ToString()));
                                 });

                                 options.AddPolicy(
                                     "Users.Delete",
                                     policy =>
                                     {
                                         policy.RequireAuthenticatedUser();
                                         policy.AddRequirements(
                                             new PermissionRequirement(
                                                 GranularPermission.Users_Delete.ToString()));
                                     });

                                     options.AddPolicy(
                                         "Sites.View",
                                         policy =>
                                         {
                                             policy.RequireAuthenticatedUser();
                                             policy.AddRequirements(
                                                 new PermissionRequirement(
                                                     GranularPermission.Sites_View.ToString()));
                                         });

                                         options.AddPolicy(
                                             "Sites.Manage",
                                             policy =>
                                             {
                                                 policy.RequireAuthenticatedUser();
                                                 policy.AddRequirements(
                                                     new PermissionRequirement(
                                                         GranularPermission.Sites_Manage.ToString()));
                                             });

                                             options.AddPolicy(
                                                 "Projects.View",
                                                 policy =>
                                                 {
                                                     policy.RequireAuthenticatedUser();
                                                     policy.AddRequirements(
                                                         new PermissionRequirement(
                                                             GranularPermission.Projects_View.ToString()));
                                                 });

                                             options.AddPolicy(
                                                 "Projects.Manage",
                                                 policy =>
                                                 {
                                                     policy.RequireAuthenticatedUser();
                                                     policy.AddRequirements(
                                                         new PermissionRequirement(
                                                             GranularPermission.Projects_Manage.ToString()));
                                                 });

                                                 options.AddPolicy(
                                                     "Finance.CostCode.View",
                                                     policy =>
                                                     {
                                                         policy.RequireAuthenticatedUser();
                                                         policy.AddRequirements(
                                                             new PermissionRequirement(
                                                                 GranularPermission.Finance_CostCode_View.ToString()));
                                                     });

                                                 options.AddPolicy(
                                                     "Finance.CostCode.Manage",
                                                     policy =>
                                                     {
                                                         policy.RequireAuthenticatedUser();
                                                         policy.AddRequirements(
                                                             new PermissionRequirement(
                                                                 GranularPermission.Finance_CostCode_Manage.ToString()));
                                                     });

                                                 options.AddPolicy(
                                                     "Finance.Budget.View",
                                                     policy =>
                                                     {
                                                         policy.RequireAuthenticatedUser();
                                                         policy.AddRequirements(
                                                             new PermissionRequirement(
                                                                 GranularPermission.Finance_Budget_View.ToString()));
                                                     });

                                                 options.AddPolicy(
                                                     "Finance.Budget.Manage",
                                                     policy =>
                                                     {
                                                         policy.RequireAuthenticatedUser();
                                                         policy.AddRequirements(
                                                             new PermissionRequirement(
                                                                 GranularPermission.Finance_Budget_Manage.ToString()));
                                                     });

                                                     options.AddPolicy(
                                                         "Finance.Invoice.View",
                                                         policy =>
                                                         {
                                                             policy.RequireAuthenticatedUser();
                                                             policy.AddRequirements(
                                                                 new PermissionRequirement(
                                                                     GranularPermission.Finance_Invoice_View.ToString()));
                                                         });

                                                     options.AddPolicy(
                                                         "Finance.Payment.View",
                                                         policy =>
                                                         {
                                                             policy.RequireAuthenticatedUser();
                                                             policy.AddRequirements(
                                                                 new PermissionRequirement(
                                                                     GranularPermission.Finance_Payment_View.ToString()));
                                                         });

                                                     options.AddPolicy(
                                                         "Finance.Payment.Manage",
                                                         policy =>
                                                         {
                                                             policy.RequireAuthenticatedUser();
                                                             policy.AddRequirements(
                                                                 new PermissionRequirement(
                                                                     GranularPermission.Finance_Payment_Manage.ToString()));
                                                         });

                                                         options.AddPolicy(
                                                             "Finance.Expense.View",
                                                             policy =>
                                                             {
                                                                 policy.RequireAuthenticatedUser();
                                                                 policy.AddRequirements(
                                                                     new PermissionRequirement(
                                                                         GranularPermission.Finance_Expense_View.ToString()));
                                                             });

                                                         options.AddPolicy(
                                                             "Finance.Expense.Submit",
                                                             policy =>
                                                             {
                                                                 policy.RequireAuthenticatedUser();
                                                                 policy.AddRequirements(
                                                                     new PermissionRequirement(
                                                                         GranularPermission.Finance_Expense_Submit.ToString()));
                                                             });

                                                         options.AddPolicy(
                                                             "Finance.Expense.Approve",
                                                             policy =>
                                                             {
                                                                 policy.RequireAuthenticatedUser();
                                                                 policy.AddRequirements(
                                                                     new PermissionRequirement(
                                                                         GranularPermission.Finance_Expense_Approve.ToString()));
                                                             });

                                                             options.AddPolicy(
                                                                 "Finance.PettyCash.View",
                                                                 policy =>
                                                                 {
                                                                     policy.RequireAuthenticatedUser();
                                                                     policy.AddRequirements(
                                                                         new PermissionRequirement(
                                                                             GranularPermission.Finance_PettyCash_View.ToString()));
                                                                 });

                                                             options.AddPolicy(
                                                                 "Finance.PettyCash.Manage",
                                                                 policy =>
                                                                 {
                                                                     policy.RequireAuthenticatedUser();
                                                                     policy.AddRequirements(
                                                                         new PermissionRequirement(
                                                                             GranularPermission.Finance_PettyCash_Manage.ToString()));
                                                                 });

                                                                 options.AddPolicy(
                                                                     "Procurement.Inventory.View",
                                                                     policy =>
                                                                     {
                                                                         policy.RequireAuthenticatedUser();
                                                                         policy.AddRequirements(
                                                                             new PermissionRequirement(
                                                                                 GranularPermission.Procurement_Inventory_View.ToString()));
                                                                     });

                                                                 options.AddPolicy(
                                                                     "Procurement.Inventory.Adjust",
                                                                     policy =>
                                                                     {
                                                                         policy.RequireAuthenticatedUser();
                                                                         policy.AddRequirements(
                                                                             new PermissionRequirement(
                                                                                 GranularPermission.Procurement_Inventory_Adjust.ToString()));
                                                                     });

                                                                 options.AddPolicy(
                                                                     "Procurement.PurchaseOrder.View",
                                                                     policy =>
                                                                     {
                                                                         policy.RequireAuthenticatedUser();
                                                                         policy.AddRequirements(
                                                                             new PermissionRequirement(
                                                                                 GranularPermission.Procurement_PurchaseOrder_View.ToString()));
                                                                     });

                                                                 options.AddPolicy(
                                                                     "Procurement.PurchaseOrder.Manage",
                                                                     policy =>
                                                                     {
                                                                         policy.RequireAuthenticatedUser();
                                                                         policy.AddRequirements(
                                                                             new PermissionRequirement(
                                                                                 GranularPermission.Procurement_PurchaseOrder_Manage.ToString()));
                                                                     });

                                                                 options.AddPolicy(
                                                                     "Procurement.PurchaseOrder.Approve",
                                                                     policy =>
                                                                     {
                                                                         policy.RequireAuthenticatedUser();
                                                                         policy.AddRequirements(
                                                                             new PermissionRequirement(
                                                                                 GranularPermission.Procurement_PurchaseOrder_Approve.ToString()));
                                                                     });

                                                                options.AddPolicy(
                                                                    "Procurement.MaterialRequest.View",
                                                                    policy =>
                                                                    {
                                                                        policy.RequireAuthenticatedUser();
                                                                        policy.AddRequirements(
                                                                            new PermissionRequirement(
                                                                                GranularPermission.Procurement_MaterialRequest_View.ToString()));
                                                                    });

                                                                options.AddPolicy(
                                                                    "Procurement.MaterialRequest.Submit",
                                                                    policy =>
                                                                    {
                                                                        policy.RequireAuthenticatedUser();
                                                                        policy.AddRequirements(
                                                                            new PermissionRequirement(
                                                                                GranularPermission.Procurement_MaterialRequest_Submit.ToString()));
                                                                    });

                                                                options.AddPolicy(
                                                                    "Procurement.MaterialRequest.Approve",
                                                                    policy =>
                                                                    {
                                                                        policy.RequireAuthenticatedUser();
                                                                        policy.AddRequirements(
                                                                            new PermissionRequirement(
                                                                                GranularPermission.Procurement_MaterialRequest_Approve.ToString()));
                                                                    });

                                                                 options.AddPolicy(
                                                                     "Reports.View",
                                                                     policy =>
                                                                     {
                                                                         policy.RequireAuthenticatedUser();
                                                                         policy.AddRequirements(
                                                                             new PermissionRequirement(
                                                                                 GranularPermission.Reports_View.ToString()));
                                                                     });

                                                                 options.AddPolicy(
                                                                     "Reports.FinancialSummary.View",
                                                                     policy =>
                                                                     {
                                                                         policy.RequireAuthenticatedUser();
                                                                         policy.AddRequirements(
                                                                             new PermissionRequirement(
                                                                                 GranularPermission.Reports_FinancialSummary_View.ToString()));
                                                                     });

                                                                 options.AddPolicy(
                                                                     "Reports.Export",
                                                                     policy =>
                                                                     {
                                                                         policy.RequireAuthenticatedUser();
                                                                         policy.AddRequirements(
                                                                             new PermissionRequirement(
                                                                                 GranularPermission.Reports_Export.ToString()));
                                                                     });
        options.AddPolicy(
            "HRM.Payroll.View",
            policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(
                    new PermissionRequirement(
                        GranularPermission.HRM_Payroll_View.ToString()));
            });

        options.AddPolicy(
            "HRM.Payroll.Approve",
            policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(
                    new PermissionRequirement(
                        GranularPermission.HRM_Payroll_Approve.ToString()));
            });

            options.AddPolicy(
                "HRM.SiteAssignment.View",
                policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.AddRequirements(
                        new PermissionRequirement(
                            GranularPermission.HRM_SiteAssignment_View.ToString()));
                });

            options.AddPolicy(
                "HRM.SiteAssignment.Manage",
                policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.AddRequirements(
                        new PermissionRequirement(
                            GranularPermission.HRM_SiteAssignment_Manage.ToString()));
                });

        });




        return services;
    }
}









































