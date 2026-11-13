using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using CSM.Web.Models.Equipment;
using CSM.Web.Services;
using Xunit;
namespace CSM.Infrastructure.Tests;
public sealed class EquipmentCreateTransportTests
{
    [Theory]
    [InlineData(400, "Equipment code already exists.", "Equipment code already exists.")]
    [InlineData(401, "private diagnostic", "Your session is missing or has expired. Please sign in again.")]
    [InlineData(403, "private diagnostic", "You do not have permission to create equipment.")]
    [InlineData(500, "private diagnostic", "Unable to save equipment. Please try again.")]
    public async Task Create_failure_has_typed_status_and_safe_message(int status, string body, string expected)
    {
        var service = Service(new ResponseHandler((HttpStatusCode)status, JsonSerializer.Serialize(new {message=body})));
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => service.CreateEquipmentAsync(new()));
        Assert.Equal((HttpStatusCode)status, error.StatusCode); Assert.Equal(expected, error.Message);
    }
    [Theory]
    [InlineData("null")] [InlineData("{}")] [InlineData("not json")]
    public async Task Invalid_success_does_not_navigate_to_an_empty_equipment_id(string body)
    {
        var service = Service(new ResponseHandler(HttpStatusCode.OK, body));
        await Assert.ThrowsAsync<HttpRequestException>(() => service.CreateEquipmentAsync(new()));
    }
    [Fact]
    public async Task Create_transmits_selected_company_and_returns_equipment_id()
    {
        var id=Guid.NewGuid();var company=Guid.NewGuid();
        var handler=new ResponseHandler(HttpStatusCode.Created, JsonSerializer.Serialize(new {id}));
        Assert.Equal(id,await Service(handler).CreateEquipmentAsync(new(){CompanyId=company,EquipmentCode="EQ-005",Name="Compactor"}));
        using var payload=JsonDocument.Parse(handler.Body!);
        Assert.Equal(company,payload.RootElement.GetProperty("companyId").GetGuid());
    }
    private static EquipmentService Service(ResponseHandler handler)
    {
        var auth=DispatchProxy.Create<IAuthService, Auth>();
        return new(new Factory(new HttpClient(handler){BaseAddress=new Uri("http://fixture/")}),auth);
    }
    private sealed record Factory(HttpClient Client):IHttpClientFactory {public HttpClient CreateClient(string name)=>Client;}
    public class Auth:DispatchProxy {protected override object? Invoke(MethodInfo? method,object?[]? args)=>Task.CompletedTask;}
    private sealed class ResponseHandler(HttpStatusCode status,string content):HttpMessageHandler
    {
        public string? Body;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
        {
            Body=request.Content is null?null:await request.Content.ReadAsStringAsync(cancellationToken);
            return new(status){Content=new StringContent(content,Encoding.UTF8,"application/json")};
        }
    }
}
