using System.Net;
using System.Reflection;
using System.Text;
using CSM.Web.Models.Equipment;
using CSM.Web.Services;
using Xunit;
namespace CSM.Infrastructure.Tests;

public sealed class MaintenanceCreateTransportTests
{
    [Theory]
    [InlineData(400, "{\"message\":\"Meter reading cannot be negative.\"}", "Meter reading cannot be negative.")]
    [InlineData(401, "{\"message\":\"private diagnostic\"}", "Your session is missing or has expired. Please sign in again.")]
    [InlineData(403, "{\"message\":\"private diagnostic\"}", "You do not have permission to create equipment maintenance.")]
    [InlineData(500, "{\"message\":\"private diagnostic\"}", "Unable to save maintenance. Please try again.")]
    [InlineData(400, "not JSON private diagnostic", "Unable to save maintenance. Please try again.")]
    public async Task Failed_maintenance_save_uses_safe_feedback(int status, string body, string expected)
    {
        using var client = new HttpClient(new Handler((HttpStatusCode)status, body)) { BaseAddress = new("https://fixture.test/api/") };
        var service = new EquipmentService(new Factory(client), DispatchProxy.Create<IAuthService, Auth>());
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => service.CreateMaintenanceAsync(new()));
        Assert.Equal((HttpStatusCode)status, error.StatusCode); Assert.Equal(expected, error.Message);
    }
    [Theory] [InlineData("null")] [InlineData("{}")] [InlineData("not JSON")]
    public async Task Malformed_success_does_not_confirm_creation(string body)
    {
        using var client = new HttpClient(new Handler(HttpStatusCode.Created, body)) { BaseAddress = new("https://fixture.test/api/") };
        var service = new EquipmentService(new Factory(client), DispatchProxy.Create<IAuthService, Auth>());
        await Assert.ThrowsAsync<HttpRequestException>(() => service.CreateMaintenanceAsync(new()));
    }
    private sealed record Factory(HttpClient Client) : IHttpClientFactory { public HttpClient CreateClient(string name) => Client; }
    public class Auth : DispatchProxy { protected override object? Invoke(MethodInfo? method, object?[]? args) => Task.CompletedTask; }
    private sealed class Handler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }
}
