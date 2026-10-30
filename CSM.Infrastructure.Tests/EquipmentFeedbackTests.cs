using System.Net;
using System.Reflection;
using Bunit;
using CSM.Web.Components.Pages;
using CSM.Web.Models.Equipment;
using CSM.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class EquipmentFeedbackTests
{
    [Theory]
    [InlineData(401, "Your session is missing or has expired. Please sign in again.", false)]
    [InlineData(403, "You do not have permission to view equipment.", false)]
    [InlineData(500, "Unable to load equipment. Please try again.", true)]
    public void Equipment_list_handles_errors_without_exposing_creation(int status, string expected, bool retry)
    {
        using var context = Context(out var service);
        service.Status = (HttpStatusCode)status;
        var page = context.RenderComponent<EquipmentList>();
        page.WaitForAssertion(() => Assert.Equal(expected, page.Find("[role=alert]").TextContent.Trim()));
        Assert.Equal(retry, page.FindAll("#retry-equipment").Count == 1);
        Assert.Equal(status == 401, page.FindAll("#equipment-sign-in").Count == 1);
        Assert.Empty(page.FindAll(".spinner-border"));
        Assert.DoesNotContain("New Equipment", page.Markup);
        Assert.DoesNotContain("No equipment found.", page.Markup);
        Assert.DoesNotContain("private API diagnostic", page.Markup);
        Assert.Contains("Back", page.Markup);
    }

    [Fact]
    public void Equipment_list_can_retry_a_transient_error()
    {
        using var context = Context(out var service);
        service.Status = HttpStatusCode.ServiceUnavailable;
        var page = context.RenderComponent<EquipmentList>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("[role=alert]")));
        service.Status = null;
        page.Find("#retry-equipment").Click();
        page.WaitForAssertion(() => Assert.Contains("Excavator", page.Markup));
        Assert.Contains("New Equipment", page.Markup);
        Assert.Empty(page.FindAll("[role=alert]"));
    }

    [Fact]
    public void Successful_empty_equipment_list_preserves_creation_for_allowed_users()
    {
        using var context = Context(out var service);
        service.Empty = true;
        var page = context.RenderComponent<EquipmentList>();
        page.WaitForAssertion(() => Assert.Contains("No equipment found.", page.Markup));
        Assert.Contains("New Equipment", page.Markup);
        Assert.Empty(page.FindAll("[role=alert]"));
    }

    [Theory]
    [InlineData(401, "Your session is missing or has expired. Please sign in again.", false)]
    [InlineData(403, "You do not have permission to create equipment.", false)]
    [InlineData(500, "Unable to verify equipment access. Please try again.", true)]
    public void Direct_create_route_checks_equipment_API_access_before_showing_form(int status, string expected, bool retry)
    {
        using var context = Context(out var service);
        service.Status = (HttpStatusCode)status;
        var page = context.RenderComponent<EquipmentCreate>();
        page.WaitForAssertion(() => Assert.Equal(expected, page.Find("[role=alert]").TextContent.Trim()));
        Assert.Empty(page.FindAll("form, input, select, textarea"));
        Assert.Equal(retry, page.FindAll("#retry-equipment-create-access").Count == 1);
        Assert.Equal(status == 401, page.FindAll("#equipment-create-sign-in").Count == 1);
        Assert.DoesNotContain("private API diagnostic", page.Markup);
        Assert.Contains("Back", page.Markup);
    }

    [Fact]
    public void Equipment_create_form_is_available_after_successful_API_access_check()
    {
        using var context = Context(out _);
        var page = context.RenderComponent<EquipmentCreate>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("form")));
        Assert.Contains("Create Equipment", page.Markup);
        Assert.Empty(page.FindAll("[role=alert]"));
    }

    private static TestContext Context(out EquipmentApi api)
    {
        var context = new TestContext();
        WorkspaceTestAccess.Add(context);
        var service = DispatchProxy.Create<IEquipmentService, EquipmentApi>();
        api = (EquipmentApi)(object)service;
        context.Services.AddSingleton(service);
        return context;
    }

    public class EquipmentApi : DispatchProxy
    {
        public HttpStatusCode? Status { get; set; }
        public bool Empty { get; set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name != nameof(IEquipmentService.GetEquipmentAsync))
                throw new InvalidOperationException("Unexpected equipment mutation.");
            return Status.HasValue
                ? Task.FromException<List<EquipmentResponse>>(new HttpRequestException("private API diagnostic", null, Status))
                : Task.FromResult(Empty ? new List<EquipmentResponse>() : new List<EquipmentResponse> {
                    new() { Id = Guid.NewGuid(), EquipmentCode = "EQ-001", Name = "Excavator", Status = "Available" }
                });
        }
    }
}
