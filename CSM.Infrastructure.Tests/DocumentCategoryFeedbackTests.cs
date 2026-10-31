using System.Net;
using System.Reflection;
using System.Security.Claims;
using Bunit;
using CSM.Api.Controllers;
using CSM.Application.Common.Exceptions;
using CSM.Application.Common.Security;
using CSM.Web.Components.Pages;
using CSM.Web.Models.Compliance;
using CSM.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CSM.Infrastructure.Tests;
public sealed class DocumentCategoryFeedbackTests
{
    [Theory]
    [InlineData("SuperAdmin", true)] [InlineData("CompanyAdmin", true)] [InlineData("SiteManager", true)]
    [InlineData("Supervisor", false)] [InlineData("Administrator", false)] [InlineData("Storekeeper", false)]
    [InlineData("ProcurementOfficer", false)] [InlineData("SafetyOfficer", false)]
    public async Task Controller_roles_match_existing_service_contract(string role, bool allowed)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddAuthorization(o => o.AddPolicy(AppPolicies.ProcurementManagement, p => p.RequireRole(AppPolicies.ProcurementManagementRoles)));
        using var provider = services.AddGrantedBackendPermissions(role).BuildServiceProvider();
        var actor = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "test"));
        foreach (var method in new[] { "GetAll", "GetById", "Create", "Update" })
        {
            var metadata = typeof(DocumentCategoriesController).GetCustomAttributes(true).OfType<IAuthorizeData>()
                .Concat(typeof(DocumentCategoriesController).GetMethod(method)!.GetCustomAttributes(true).OfType<IAuthorizeData>());
            var policy = await AuthorizationPolicy.CombineAsync(provider.GetRequiredService<IAuthorizationPolicyProvider>(), metadata);
            Assert.Equal(allowed, (await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(BackendAuthorizationFixture.WithGrantedIdentity(actor), null, policy!)).Succeeded);
        }
    }

    [Theory]
    [InlineData("You are not authorized to access this document category.", 403)]
    [InlineData("Document category was not found.", 404)]
    [InlineData("Document category code is required.", 400)]
    public async Task Controller_preserves_access_missing_and_validation_semantics(string message, int expected)
    {
        var api = DispatchProxy.Create<CSM.Application.Compliance.DocumentCategories.IDocumentCategoryService, DomainApi>();
        ((DomainApi)(object)api).Message = message;
        var controller = new DocumentCategoriesController(api) { ControllerContext = new ControllerContext {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())], "test")) } } };
        var result = await controller.GetById(Guid.NewGuid(), default);
        Assert.Equal(expected, Assert.IsAssignableFrom<ObjectResult>(result.Result).StatusCode);
    }

    [Theory]
    [InlineData("list", 401)] [InlineData("list", 403)] [InlineData("list", 500)]
    [InlineData("create", 401)] [InlineData("create", 403)] [InlineData("create", 500)]
    [InlineData("edit", 401)] [InlineData("edit", 403)] [InlineData("edit", 500)]
    public void Protected_load_failure_has_no_partial_form_data_or_mutation_button(string route, int status)
    {
        using var context = Context(out var state);
        state.Status = (HttpStatusCode)status;
        var page = Render(context, route);
        page.WaitForAssertion(() => Assert.Single(page.FindAll("[role=alert]")));
        Assert.Empty(page.FindAll("table, form, input, select, textarea, dl"));
        Assert.DoesNotContain("No document categories found", page.Markup);
        Assert.DoesNotContain("private diagnostic", page.Markup);
        Assert.DoesNotContain(page.FindAll("button"), b => b.TextContent.Trim() is "Add Category" or "Edit");
        Assert.Equal(status == 401, page.FindAll("#category-" + route + "-sign-in").Count == 1);
        Assert.Equal(status == 500, page.FindAll("#retry-category-" + route).Count == 1);
    }

    [Theory]
    [InlineData("list")] [InlineData("create")] [InlineData("edit")]
    public void Retry_recovers_protected_data_without_saving(string route)
    {
        using var context = Context(out var state);
        state.Status = HttpStatusCode.ServiceUnavailable;
        var page = Render(context, route);
        page.WaitForAssertion(() => Assert.Single(page.FindAll("#retry-category-" + route)));
        state.Status = null;
        page.Find("#retry-category-" + route).Click();
        page.WaitForAssertion(() => Assert.Contains(route == "create" ? "Category Code" : "CAT-TEST-1", page.Markup));
        Assert.Empty(page.FindAll("[role=alert]"));
    }

    [Theory]
    [InlineData("edit")]
    public void Missing_category_has_no_form_or_retry(string route)
    {
        using var context = Context(out var state);
        state.Missing = true;
        var page = Render(context, route);
        page.WaitForAssertion(() => Assert.Contains("Document category was not found.", page.Markup));
        Assert.Empty(page.FindAll("form, input, select, textarea, #retry-category-" + route));
    }

    private static IRenderedFragment Render(TestContext context, string route) => route switch {
        "list" => context.RenderComponent<DocumentCategories>(),
        "create" => context.RenderComponent<DocumentCategoryCreate>(),
        _ => context.RenderComponent<DocumentCategoryEdit>(p => p.Add(x => x.Id, Guid.Parse("11111111-1111-1111-1111-111111111111")))
    };
    private static TestContext Context(out State state)
    {
        var context = new TestContext(); state = new();
        var api = DispatchProxy.Create<IComplianceService, Api>(); ((Api)(object)api).State = state;
        context.Services.AddSingleton(api);
        return context;
    }
    public sealed class State { public HttpStatusCode? Status; public bool Missing; }
    public class Api : DispatchProxy
    {
        public State State = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            var row = new DocumentCategoryResponse { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                CategoryCode = "CAT-TEST-1", Name = "Test Category", IsActive = true };
            if (method!.Name == nameof(IComplianceService.GetDocumentCategoriesAsync))
                return State.Status.HasValue ? Task.FromException<IReadOnlyList<DocumentCategoryResponse>>(Error(State.Status.Value))
                    : Task.FromResult<IReadOnlyList<DocumentCategoryResponse>>(new[] { row });
            if (method.Name == nameof(IComplianceService.GetDocumentCategoryAsync))
                return State.Status.HasValue ? Task.FromException<DocumentCategoryResponse?>(Error(State.Status.Value))
                    : Task.FromResult<DocumentCategoryResponse?>(State.Missing ? null : row);
            throw new InvalidOperationException("Unexpected category mutation.");
        }
    }
    public class DomainApi : DispatchProxy
    {
        public string Message = "";
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            Task.FromException<CSM.Application.Compliance.DocumentCategories.Dtos.DocumentCategoryResponse>(new DocumentCategoryManagementException(Message));
    }
    private static HttpRequestException Error(HttpStatusCode status) => new("private diagnostic", null, status);
}
