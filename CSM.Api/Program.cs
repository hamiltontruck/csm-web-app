using CSM.Infrastructure;
using CSM.Infrastructure.Persistence;
using System.Text.Json.Serialization;
using CSM.Application.Finance.Invoices;
using CSM.Application.Finance.Payments;
using CSM.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Infrastructure
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<NotificationHelper>();
builder.Services.AddScoped<EquipmentFuelLogService>();
builder.Services.AddScoped<EquipmentHourLogService>();
builder.Services.AddScoped<EquipmentUtilizationService>();
builder.Services.AddScoped<ComplianceDashboardService>();
builder.Services.AddScoped<ComplianceViolationService>();
builder.Services.AddScoped<ComplianceAlertService>();
builder.Services.AddScoped<ComplianceReportService>();
builder.Services.AddScoped<IInvoicePdfService, InvoicePdfService>();
builder.Services.AddScoped<
    IPaymentReceiptPdfService,
    PaymentReceiptPdfService>();

// Controllers
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter());
    });




// Built-in OpenAPI (.NET 10)
builder.Services.AddOpenApi();

builder.Services.AddCors(options =>
{
    options.AddPolicy("FlutterWeb", policy =>
    {
        var origins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? [];

        if (origins.Length > 0)
        {
            policy.WithOrigins(origins);
        }

        policy.AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

// OpenAPI endpoint
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();

app.UseCors("FlutterWeb");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

await DatabaseInitializer.InitializeAsync(app.Services);

app.Run();