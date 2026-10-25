using System.Net;
using System.Reflection;
using Bunit;
using CSM.Web.Components.Pages;
using CSM.Web.Models.Procurement;
using CSM.Web.Models.Sites;
using CSM.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class MaterialReturnAuditFeedbackTests
{
    private static readonly Guid SiteId = Guid.NewGuid();

    [Theory]
    [InlineData(401, "Your session is missing or has expired. Please sign in again.", false)]
    [InlineData(403, "You do not have permission to view material return audit history.", false)]
    [InlineData(500, "Unable to load material return audit history. Please try again.", true)]
    public void Audit_history_handles_API_errors_without_crashing(int status, string expected, bool retry)
    {
        using var context = Context(out var inventory, out _);
        inventory.Status = (HttpStatusCode)status;
        var page = context.RenderComponent<MaterialReturnAuditSite>(p => p.Add(x => x.SiteId, SiteId));
        page.WaitForAssertion(() => Assert.Equal(expected, page.Find("[role=alert]").TextContent.Trim()));
        Assert.Equal(retry, page.FindAll("#retry-material-return-audit").Count == 1);
        Assert.Equal(status == 401, page.FindAll("#material-return-audit-sign-in").Count == 1);
        Assert.Contains("Back", page.Markup);
        Assert.DoesNotContain("Loading audit history...", page.Markup);
        Assert.DoesNotContain("No material return history found.", page.Markup);
        Assert.DoesNotContain("private API diagnostic", page.Markup);
    }

    [Fact]
    public void Audit_history_can_retry_and_filters_returns_to_the_selected_site()
    {
        using var context = Context(out var inventory, out _);
        inventory.Status = HttpStatusCode.ServiceUnavailable;
        var page = context.RenderComponent<MaterialReturnAuditSite>(p => p.Add(x => x.SiteId, SiteId));
        page.WaitForAssertion(() => Assert.Single(page.FindAll("[role=alert]")));
        inventory.Status = null;
        page.Find("#retry-material-return-audit").Click();
        page.WaitForAssertion(() => Assert.Contains("RET-001", page.Markup));
        Assert.Contains("Hamilton Test Site", page.Markup);
        Assert.Contains("Cement", page.Markup);
        Assert.DoesNotContain("Other site material", page.Markup);
        Assert.Empty(page.FindAll("[role=alert]"));
    }

    [Fact]
    public void Successful_empty_audit_shows_empty_state()
    {
        using var context = Context(out var inventory, out _);
        inventory.Empty = true;
        var page = context.RenderComponent<MaterialReturnAuditSite>(p => p.Add(x => x.SiteId, SiteId));
        page.WaitForAssertion(() => Assert.Contains("No material return history found.", page.Markup));
        Assert.Empty(page.FindAll("[role=alert]"));
    }

    [Theory]
    [InlineData(401, "Your session is missing or has expired. Please sign in again.", false)]
    [InlineData(403, "You do not have permission to view construction sites.", false)]
    [InlineData(500, "Unable to load construction sites. Please try again.", true)]
    public void Audit_site_picker_handles_API_errors_without_crashing(int status, string expected, bool retry)
    {
        using var context = Context(out _, out var sites);
        sites.Status = (HttpStatusCode)status;
        var page = context.RenderComponent<MaterialReturnAudit>();
        page.WaitForAssertion(() => Assert.Equal(expected, page.Find("[role=alert]").TextContent.Trim()));
        Assert.Equal(retry, page.FindAll("#retry-material-return-audit-sites").Count == 1);
        Assert.Equal(status == 401, page.FindAll("#material-return-audit-sites-sign-in").Count == 1);
        Assert.Contains("Back", page.Markup);
        Assert.DoesNotContain("Loading construction sites...", page.Markup);
        Assert.DoesNotContain("private API diagnostic", page.Markup);
    }

    [Fact]
    public void Audit_site_picker_can_retry_after_temporary_failure()
    {
        using var context = Context(out _, out var sites);
        sites.Status = HttpStatusCode.ServiceUnavailable;
        var page = context.RenderComponent<MaterialReturnAudit>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("[role=alert]")));
        sites.Status = null;
        page.Find("#retry-material-return-audit-sites").Click();
        page.WaitForAssertion(() => Assert.Contains("Hamilton Test Site", page.Markup));
        Assert.Empty(page.FindAll("[role=alert]"));
    }

    private static TestContext Context(out Inventory inventory, out Sites sites)
    {
        var context = new TestContext();
        var stock = DispatchProxy.Create<IInventoryService, Inventory>();
        inventory = (Inventory)(object)stock;
        var projects = DispatchProxy.Create<IProjectService, Sites>();
        sites = (Sites)(object)projects;
        context.Services.AddSingleton(stock);
        context.Services.AddSingleton(projects);
        return context;
    }

    public class Inventory : DispatchProxy
    {
        public HttpStatusCode? Status { get; set; }
        public bool Empty { get; set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            Status.HasValue
                ? Task.FromException<List<StockReturnResponse>>(new HttpRequestException("private API diagnostic", null, Status))
                : Task.FromResult(Empty ? new List<StockReturnResponse>() : new List<StockReturnResponse> {
                    new() { ConstructionSiteId = SiteId, SiteName = "Hamilton Test Site", MaterialCode = "MAT-001",
                        MaterialName = "Cement", ReferenceNumber = "RET-001", ReturnedQuantity = 1m,
                        OriginalIssuedQuantity = 2m, RemainingReturnableQuantity = 1m, UnitOfMeasure = "Bag",
                        ReturnDateUtc = new DateTime(2026, 10, 21, 0, 0, 0, DateTimeKind.Utc) },
                    new() { ConstructionSiteId = Guid.NewGuid(), MaterialName = "Other site material" }
                });
    }

    public class Sites : DispatchProxy
    {
        public HttpStatusCode? Status { get; set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            Status.HasValue
                ? Task.FromException<List<SiteResponse>>(new HttpRequestException("private API diagnostic", null, Status))
                : Task.FromResult(new List<SiteResponse> { new() { Id = SiteId, SiteCode = "HBG-TEST-001", Name = "Hamilton Test Site" } });
    }
}
