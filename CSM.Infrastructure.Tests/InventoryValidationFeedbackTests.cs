using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using Bunit;
using CSM.Web.Components.Pages;
using CSM.Web.Models.Procurement;
using CSM.Web.Models.Sites;
using CSM.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class InventoryValidationFeedbackTests
{
    public const string OverReturn = "Return quantity exceeds the remaining returnable quantity for the original issue.";
    public const string Duplicate = "A stock return with this reference number already exists in the company.";
    public const string OverIssue = "Issue quantity exceeds available stock.";
    private static readonly Guid SiteId = Guid.NewGuid();
    private static readonly Guid MaterialId = Guid.NewGuid();
    private static readonly Guid IssueId = Guid.NewGuid();

    [Theory]
    [InlineData(false, OverIssue)]
    [InlineData(true, OverReturn)]
    [InlineData(true, Duplicate)]
    public async Task Web_client_preserves_API_validation_message(bool returns, string message)
    {
        using var handler = new Api { Message = message };
        using var http = Client(handler);
        var service = Service(http);
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => Save(service, returns));
        Assert.Equal(HttpStatusCode.BadRequest, error.StatusCode);
        Assert.Equal(message, error.Message);
    }

    [Theory]
    [InlineData(false, 401, "Your session has expired. Please sign in again.")]
    [InlineData(true, 403, "You do not have permission to create stock returns.")]
    [InlineData(false, 403, "You do not have permission to create stock issues.")]
    [InlineData(true, 500, "The inventory service is temporarily unavailable. Please try again in a minute.")]
    [InlineData(false, 502, "The inventory service is temporarily unavailable. Please try again in a minute.")]
    public async Task Web_client_uses_clear_safe_messages_for_auth_and_server_errors(bool returns, int status, string expected)
    {
        using var handler = new Api { Status = (HttpStatusCode)status, RawBody = "<html>private server exception</html>" };
        using var http = Client(handler);
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => Save(Service(http), returns));
        Assert.Equal(expected, error.Message);
        Assert.Equal((HttpStatusCode)status, error.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Malformed_validation_body_uses_fallback(bool returns)
    {
        using var handler = new Api { RawBody = "<html>bad gateway</html>" };
        using var http = Client(handler);
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => Save(Service(http), returns));
        Assert.Equal(returns ? "Unable to create stock return." : "Unable to create stock issue.", error.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Model_validation_errors_are_read_from_problem_details(bool returns)
    {
        using var handler = new Api { RawBody = "{\"errors\":{\"ReferenceNumber\":[\"Reference number is required.\"]}}" };
        using var http = Client(handler);
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => Save(Service(http), returns));
        Assert.Equal("Reference number is required.", error.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void API_validation_is_shown_once_and_fields_survive_retry(bool returns)
    {
        using var handler = new Api { Message = returns ? OverReturn : OverIssue };
        using var context = Context(handler);
        IRenderedFragment page = returns
            ? context.RenderComponent<StockReturnCreate>(p => p.Add(x => x.SiteId, SiteId))
            : context.RenderComponent<StockIssueCreate>(p => p.Add(x => x.SiteId, (Guid?)SiteId));
        Fill(page, returns);
        SaveButton(page).Click();
        page.WaitForAssertion(() =>
            Assert.Equal(handler.Message, Assert.Single(page.FindAll(".alert-danger")).TextContent.Trim()));
        Assert.Single(page.FindAll("[role=alert]"));
        Assert.Equal("TEST-001", page.Find("input:not([type])").GetAttribute("value"));
        Assert.Equal("2", page.Find("input[type=number]").GetAttribute("value"));
        Assert.Equal("keep my remarks", page.Find("textarea").GetAttribute("value"));
        Assert.False(SaveButton(page).HasAttribute("disabled"));
        Assert.DoesNotContain("/stock-returns/site/", context.Services.GetRequiredService<NavigationManager>().Uri);

        handler.Status = HttpStatusCode.OK;
        SaveButton(page).Click();
        page.WaitForAssertion(() => Assert.EndsWith(
            returns ? $"/stock-returns/site/{SiteId}" : $"/stock-issues/site/{SiteId}",
            context.Services.GetRequiredService<NavigationManager>().Uri));
        Assert.Equal(2, handler.Posts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Network_failure_keeps_form_usable_without_exposing_exception(bool returns)
    {
        using var handler = new Api { ThrowOnPost = true };
        using var context = Context(handler);
        IRenderedFragment page = returns
            ? context.RenderComponent<StockReturnCreate>(p => p.Add(x => x.SiteId, SiteId))
            : context.RenderComponent<StockIssueCreate>(p => p.Add(x => x.SiteId, (Guid?)SiteId));
        Fill(page, returns);
        SaveButton(page).Click();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("[role=alert]")));
        Assert.DoesNotContain("private network exception", page.Markup);
        Assert.Equal("TEST-001", page.Find("input:not([type])").GetAttribute("value"));
        Assert.False(SaveButton(page).HasAttribute("disabled"));
    }


    [Theory]
    [InlineData("Insufficient available stock.")]
    [InlineData("A stock transfer with this transfer number already exists.")]
    public async Task Transfer_client_preserves_validation_message(string message)
    {
        using var handler = new Api { Message = message };
        using var http = Client(handler);
        var error = await Assert.ThrowsAsync<HttpRequestException>(() =>
            Service(http).CreateStockTransferAsync(new()));
        Assert.Equal(HttpStatusCode.BadRequest, error.StatusCode);
        Assert.Equal(message, error.Message);
    }

    [Theory]
    [InlineData(401, "Your session has expired. Please sign in again.")]
    [InlineData(403, "You do not have permission to create stock transfers.")]
    [InlineData(500, "The inventory service is temporarily unavailable. Please try again in a minute.")]
    [InlineData(400, "Unable to create stock transfer.")]
    public async Task Transfer_client_uses_safe_fallbacks(int status, string expected)
    {
        using var handler = new Api { Status = (HttpStatusCode)status, RawBody = "<html>private server exception</html>" };
        using var http = Client(handler);
        var error = await Assert.ThrowsAsync<HttpRequestException>(() =>
            Service(http).CreateStockTransferAsync(new()));
        Assert.Equal(expected, error.Message);
    }

    [Theory]
    [InlineData("Insufficient available stock.", false)]
    [InlineData("A stock transfer with this transfer number already exists.", false)]
    [InlineData("Unable to reach the inventory service. Please try again.", true)]
    public void Transfer_failure_shows_one_alert_preserves_fields_and_allows_retry(string message, bool network)
    {
        using var handler = new Api { Message = message, ThrowOnPost = network };
        using var context = Context(handler);
        var page = context.RenderComponent<StockTransferCreate>();
        page.WaitForAssertion(() => Assert.True(page.FindAll("option").Count >= 3));
        page.FindAll("select").ToArray()[0].Change(SiteId.ToString());
        page.FindAll("select").ToArray()[1].Change(Guid.NewGuid().ToString());
        page.WaitForAssertion(() => Assert.Contains("Cement", page.Markup));
        page.FindAll("select").ToArray()[2].Change(MaterialId.ToString());
        page.Find("input[type=number]").Change("7");
        page.FindAll("input:not([type])").ToArray()[0].Change("TRF-TEST-001");
        page.Find("textarea").Change("keep my remarks");
        SaveButton(page).Click();
        page.WaitForAssertion(() => Assert.Equal(message, Assert.Single(page.FindAll("[role=alert]")).TextContent.Trim()));
        Assert.Equal("TRF-TEST-001", page.FindAll("input:not([type])").ToArray()[0].GetAttribute("value"));
        Assert.Equal("7", page.Find("input[type=number]").GetAttribute("value"));
        Assert.Equal("keep my remarks", page.Find("textarea").GetAttribute("value"));
        Assert.DoesNotContain("private network exception", page.Markup);
        Assert.False(SaveButton(page).HasAttribute("disabled"));
        Assert.DoesNotContain("/stock-transfers/site/", context.Services.GetRequiredService<NavigationManager>().Uri);
        handler.ThrowOnPost = false;
        handler.Status = HttpStatusCode.OK;
        SaveButton(page).Click();
        page.WaitForAssertion(() => Assert.EndsWith($"/stock-transfers/site/{SiteId}",
            context.Services.GetRequiredService<NavigationManager>().Uri));
        Assert.Equal(2, handler.Posts);
    }

    private static void Fill(IRenderedFragment page, bool returns)
    {
        page.WaitForAssertion(() => Assert.True(page.FindAll("option").Count >= 2));
        var selects = page.FindAll("select").ToArray();
        selects[^1].Change((returns ? IssueId : MaterialId).ToString());
        page.Find("input[type=number]").Change("2");
        page.Find("input:not([type])").Change("TEST-001");
        page.Find("textarea").Change("keep my remarks");
    }

    private static AngleSharp.Dom.IElement SaveButton(IRenderedFragment page) =>
        page.FindAll("button").Single(x => x.TextContent.Contains("Save") || x.TextContent.Contains("Saving"));

    private static TestContext Context(Api handler)
    {
        var context = new TestContext();
        context.Services.AddSingleton<IInventoryService>(Service(Client(handler)));
        context.Services.AddSingleton(DispatchProxy.Create<IProjectService, ProjectProxy>());
        return context;
    }

    private static HttpClient Client(Api handler) => new(handler, disposeHandler: false) { BaseAddress = new Uri("https://api.test/api/") };
    private static InventoryService Service(HttpClient http) => new(http, DispatchProxy.Create<IAuthService, AuthProxy>());
    private static Task<bool> Save(IInventoryService service, bool returns) => returns
        ? service.CreateStockReturnAsync(new() { OriginalIssueMovementId = IssueId, Quantity = 2, ReferenceNumber = "TEST-001" })
        : service.CreateStockIssueAsync(new() { ConstructionSiteId = SiteId, MaterialId = MaterialId, Quantity = 8, ReferenceNumber = "TEST-001" });

    public class AuthProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) => Task.CompletedTask;
    }

    public class ProjectProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) => Task.FromResult(new List<SiteResponse>
            { new() { Id = SiteId, SiteCode = "HBG-TEST-001", Name = "Hamilton Test Site" } });
    }

    private sealed class Api : HttpMessageHandler
    {
        public HttpStatusCode Status { get; set; } = HttpStatusCode.BadRequest;
        public string Message { get; set; } = OverReturn;
        public string? RawBody { get; set; }
        public bool ThrowOnPost { get; set; }
        public int Posts { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get)
            {
                object data = request.RequestUri!.AbsolutePath.EndsWith("StockMovements")
                    ? new List<StockMovementResponse> { new() { Id = IssueId, ConstructionSiteId = SiteId, MovementType = "Issue",
                        MaterialCode = "MAT-001", MaterialName = "Cement", Quantity = 2, UnitOfMeasure = "Bag",
                        ReferenceNumber = "ISS-2026-002", MovementDateUtc = DateTime.UtcNow } }
                    : (object)new List<StockBalanceResponse> { new() { MaterialId = MaterialId, MaterialCode = "MAT-001",
                        MaterialName = "Cement", QuantityOnHand = 7, UnitOfMeasure = "Bag" } };
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(data) });
            }
            Posts++;
            if (ThrowOnPost) throw new HttpRequestException("private network exception");
            return Task.FromResult(new HttpResponseMessage(Status) {
                Content = RawBody is null ? JsonContent.Create(new { message = Message }) : new StringContent(RawBody, Encoding.UTF8, "text/html") });
        }
    }
}
