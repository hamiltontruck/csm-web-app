using System.Net;
using System.Reflection;
using System.Text;
using CSM.Web.Services;
using Xunit;
namespace CSM.Infrastructure.Tests;
public sealed class DailyUsageTransportTests
{
    [Theory]
    [InlineData(400,"{\"message\":\"Usage date must match the daily progress log date.\"}","Usage date must match the daily progress log date.")]
    [InlineData(401,"{\"message\":\"private diagnostic\"}","Your session has expired. Please sign in again.")]
    [InlineData(403,"{\"message\":\"private diagnostic\"}","You do not have permission to save daily material usage.")]
    [InlineData(500,"{\"message\":\"private diagnostic\"}","Unable to save daily material usage. Please try again.")]
    public async Task Rejected_save_preserves_status_and_safe_feedback(int status,string body,string expected)
    {
        using var client=new HttpClient(new Handler((HttpStatusCode)status,body)){BaseAddress=new("https://fixture.test/api/")};
        var service=new InventoryService(client,DispatchProxy.Create<IAuthService,Auth>());
        var error=await Assert.ThrowsAsync<HttpRequestException>(()=>service.CreateDailyMaterialUsageAsync(new()));Assert.Equal(expected,error.Message);Assert.Equal((HttpStatusCode)status,error.StatusCode);
    }
    [Theory] [InlineData("null")] [InlineData("{}")] [InlineData("not JSON")]
    public async Task Malformed_success_does_not_confirm_usage(string body)
    {
        using var client=new HttpClient(new Handler(HttpStatusCode.OK,body)){BaseAddress=new("https://fixture.test/api/")};
        var service=new InventoryService(client,DispatchProxy.Create<IAuthService,Auth>());
        await Assert.ThrowsAsync<HttpRequestException>(()=>service.CreateDailyMaterialUsageAsync(new()));
    }
    public class Auth:DispatchProxy{protected override object? Invoke(MethodInfo? m,object?[]? a)=>Task.CompletedTask;}
    private sealed class Handler(HttpStatusCode status,string body):HttpMessageHandler
    {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)=>Task.FromResult(new HttpResponseMessage(status){Content=new StringContent(body,Encoding.UTF8,"application/json")});}
}
