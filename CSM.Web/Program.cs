using CSM.Web.Components;
using CSM.Web.Services;
using CSM.Web.Services.HRM;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddScoped(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    return factory.CreateClient("CSM.Api");
});

var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"];
if (!Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var apiUri))
{
    throw new InvalidOperationException(
        "Set ApiSettings:BaseUrl (environment variable ApiSettings__BaseUrl) to the CSM.Api base URL.");
}

builder.Services.AddHttpClient("CSM.Api", client =>
{
    client.BaseAddress = apiUri;
});

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<AuthService>(sp =>
    (AuthService)sp.GetRequiredService<IAuthService>());
builder.Services.AddScoped<ProtectedSessionStorage>();
builder.Services.AddScoped<IExecutiveDashboardService,
    ExecutiveDashboardService>();

    builder.Services.AddScoped<IProjectService, ProjectService>();
    builder.Services.AddScoped<IProcurementService, ProcurementService>();
    builder.Services.AddScoped<IInventoryService, InventoryService>();

    builder.Services.AddScoped<IVendorService, VendorService>();

    builder.Services.AddScoped<IEquipmentService, EquipmentService>();

    builder.Services.AddScoped<UserWebService>();
    builder.Services.AddScoped<CompanyWebService>();

    builder.Services.AddScoped<RoleWebService>();

builder.Services.AddScoped<IFinanceService, FinanceService>();
builder.Services.AddScoped<INotificationApiService, NotificationApiService>();

builder.Services.AddScoped<IEmployeeService, EmployeeService>();

builder.Services.AddScoped<
    IContractorService,
    ContractorService>();

builder.Services.AddScoped<IAttendanceService, AttendanceService>();
builder.Services.AddScoped<ISiteAssignmentService, SiteAssignmentService>();
builder.Services.AddScoped<IShiftService, ShiftService>();
builder.Services.AddScoped<ILeaveService, LeaveService>();
builder.Services.AddScoped<IPayrollService, PayrollService>();
builder.Services.AddScoped<ITimesheetService, TimesheetService>();
builder.Services.AddScoped<IComplianceService, ComplianceService>();

builder.Services.AddScoped<IReportingService, ReportingService>();

builder.Services.AddScoped<IOfflineSyncStore, OfflineSyncStore>();
builder.Services.AddScoped<IOfflineSyncService, OfflineSyncService>();
builder.Services.AddScoped<
    IOfflineSyncCoordinator,
    OfflineSyncCoordinator>();

builder.Services.AddScoped<IOfflineSyncApi, OfflineSyncApi>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();