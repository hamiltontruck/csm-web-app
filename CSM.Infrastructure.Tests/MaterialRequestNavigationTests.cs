using System.Reflection;
using Bunit;
using CSM.Web.Components.Pages;
using CSM.Web.Models.Procurement;
using CSM.Web.Models.Sites;
using CSM.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class MaterialRequestNavigationTests
{
    [Fact]
    public async Task New_request_from_site_opens_form_and_saves_the_selected_site()
    {
        using var context = Context();
        context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/material-requests/site/{SiteId}");
        var page = RenderRouter(context);
        page.WaitForAssertion(() => Assert.Single(page.FindAll("button.btn-primary")));
        await page.Find("button.btn-primary").ClickAsync(new());
        page.WaitForAssertion(() => Assert.Contains("New Material Request", page.Markup));
        FillAndSubmit(page);
        var request = Assert.Single(((ProcurementProxy)(object)context.Services.GetRequiredService<IProcurementService>()).Requests);
        Assert.Equal(SiteId, request.ConstructionSiteId);
        Assert.Equal(MaterialId, request.Lines.Single().MaterialId);
        Assert.Equal(10m, request.Lines.Single().RequestedQuantity);
    }

    [Fact]
    public void Generic_create_route_still_allows_selecting_a_site()
    {
        using var context = Context();
        context.Services.GetRequiredService<NavigationManager>().NavigateTo("/material-requests/create");
        var page = RenderRouter(context);
        page.WaitForAssertion(() => Assert.Contains("New Material Request", page.Markup));
        page.Find("select").Change(SiteId.ToString());
        FillAndSubmit(page);
        Assert.Equal(SiteId, Assert.Single(((ProcurementProxy)(object)context.Services.GetRequiredService<IProcurementService>()).Requests).ConstructionSiteId);
    }

    private static IRenderedComponent<Router> RenderRouter(TestContext context)
    {
        RenderFragment<RouteData> found = data => builder =>
        {
            builder.OpenComponent(0, data.PageType);
            var sequence = 1;
            foreach (var value in data.RouteValues)
                builder.AddAttribute(sequence++, value.Key, value.Value);
            builder.CloseComponent();
        };
        return context.RenderComponent<Router>(parameters => parameters
            .Add(x => x.AppAssembly, typeof(MaterialRequestCreate).Assembly)
            .Add(x => x.Found, found)
            .Add(x => x.NotFound, builder => builder.AddContent(0, "Route not found")));
    }

    private static void FillAndSubmit(IRenderedComponent<Router> page)
    {
        page.Find("input").Change("MR-2026-001");
        page.FindAll("select").ToArray()[1].Change(MaterialId.ToString());
        page.Find("input[type=number]").Change("10");
        page.Find("form").Submit();
    }

    private static readonly Guid SiteId = Guid.NewGuid();
    private static readonly Guid MaterialId = Guid.NewGuid();

    private static TestContext Context()
    {
        var context = new TestContext();
        context.Services.AddSingleton(DispatchProxy.Create<IProjectService, ProjectProxy>());
        context.Services.AddSingleton(DispatchProxy.Create<IProcurementService, ProcurementProxy>());
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        return context;
    }

    public class ProjectProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            Task.FromResult(new List<SiteResponse> { new() { Id = SiteId, Name = "Hamilton Test Site" } });
    }

    public class ProcurementProxy : DispatchProxy
    {
        public List<CreateMaterialRequestRequest> Requests { get; } = [];
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            switch (method!.Name)
            {
                case "GetMaterialRequestsAsync": return Task.FromResult(new List<MaterialRequestResponse>());
                case "GetMaterialsAsync": return Task.FromResult(new List<MaterialResponse> { new() { Id = MaterialId, Name = "Cement" } });
                case "CreateMaterialRequestAsync":
                    Requests.Add((CreateMaterialRequestRequest)args![0]!);
                    return Task.CompletedTask;
                default: throw new NotSupportedException(method.Name);
            }
        }
    }
}
