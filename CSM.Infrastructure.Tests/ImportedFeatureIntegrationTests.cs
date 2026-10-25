using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using Bunit;
using CSM.Api.Controllers;
using CSM.Application.UserPreferences;
using CSM.Domain.Entities.Identity;
using CSM.Infrastructure;
using CSM.Infrastructure.Persistence;
using CSM.Web.Services;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class ImportedFeatureIntegrationTests
{
    [Fact]
    public void Infrastructure_registers_preferences_and_notification_services()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=test;Password=test",
            ["Jwt:SecretKey"] = "test-secret-with-at-least-thirty-two-characters",
            ["Jwt:Issuer"] = "test", ["Jwt:Audience"] = "test"
        }).Build();
        var services = new ServiceCollection().AddLogging();
        services.AddInfrastructure(config);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IUserPreferencesService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<CSM.Application.Notifications.INotificationPublisher>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<CSM.Application.Finance.Budgets.IBudgetAlertService>());
    }

    [Fact]
    public async Task Privacy_requests_persist_and_mine_excludes_other_users_and_companies()
    {
        using var db = Db();
        Assert.NotNull(db.Model.FindEntityType(typeof(PrivacyDataRequest)));
        var company = Guid.NewGuid();
        var user = new User { Id = Guid.NewGuid(), CompanyId = company, IsActive = true };
        db.Users.Add(user);
        db.SaveChanges(true);
        var controller = Controller(db, user.Id, company);
        Assert.IsType<CreatedAtActionResult>(await controller.Create(new() {
            RequestType = "datacorrection", Description = "My correction request" }, default));
        db.Set<PrivacyDataRequest>().AddRange(
            new() { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), CompanyId = company, Description = "Another user's request" },
            new() { Id = Guid.NewGuid(), UserId = user.Id, CompanyId = Guid.NewGuid(), Description = "Other company request" });
        db.SaveChanges(true);
        var mine = Assert.IsType<OkObjectResult>(await controller.GetMine(default));
        var json = System.Text.Json.JsonSerializer.Serialize(mine.Value);
        Assert.Contains("My correction request", json);
        Assert.DoesNotContain("Another user's request", json);
        Assert.DoesNotContain("Other company request", json);
        Assert.Equal("DataCorrection", db.Set<PrivacyDataRequest>().Single(x => x.Description == "My correction request").RequestType);
        var before = db.Set<PrivacyDataRequest>().Count();
        Assert.IsType<BadRequestObjectResult>(await controller.Create(new() { RequestType = "invalid" }, default));
        Assert.Equal(before, db.Set<PrivacyDataRequest>().Count());
        Assert.IsType<ForbidResult>(await Controller(db, user.Id, Guid.Empty).GetMine(default));
    }

    [Fact]
    public async Task Session_client_reads_browser_storage_without_null_dependency()
    {
        using var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddDataProtection();
        context.Services.AddScoped<ProtectedSessionStorage>();
        context.Services.AddSingleton<IHttpClientFactory>(new Factory());
        context.Services.AddSingleton(DispatchProxy.Create<CSM.Web.Services.IAuthService, AuthProxy>());
        var client = ActivatorUtilities.CreateInstance<UserSessionsService>(context.Services);
        Assert.Empty(await client.GetSessionsAsync());
    }

    private static PrivacyDataRequestsController Controller(ApplicationDbContext db, Guid user, Guid company) =>
        new(db) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext {
            User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, user.ToString()), new Claim("company_id", company.ToString())], "test")) } } };
    private static TestDb Db() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private sealed class TestDb(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
    {
        public override Task<int> SaveChangesAsync(CancellationToken token = default) => base.SaveChangesAsync(true, token);
    }
    public class AuthProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) => Task.CompletedTask;
    }
    private sealed class Factory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new Handler()) { BaseAddress = new Uri("https://api.test/api/") };
    }
    private sealed class Handler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<object>()) });
    }
}
