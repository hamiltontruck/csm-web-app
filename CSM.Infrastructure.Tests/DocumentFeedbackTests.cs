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
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Tests;
public sealed class DocumentFeedbackTests
{
    [Theory]
    [InlineData("SuperAdmin", true)] [InlineData("CompanyAdmin", true)] [InlineData("SiteManager", true)]
    [InlineData("Supervisor", false)] [InlineData("Administrator", false)] [InlineData("Storekeeper", false)]
    [InlineData("ProcurementOfficer", false)] [InlineData("SafetyOfficer", false)]
    public async Task Controller_roles_match_existing_service_contract(string role, bool allowed)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddAuthorization(o => o.AddPolicy(AppPolicies.ProcurementManagement, p => p.RequireRole(AppPolicies.ProcurementManagementRoles)));
        using var provider = services.BuildServiceProvider();
        var actor = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "test"));
        foreach (var method in new[] { "GetAll", "GetById", "Create", "Update", "UploadVersion" })
        {
            var metadata = typeof(DocumentsController).GetCustomAttributes(true).OfType<IAuthorizeData>()
                .Concat(typeof(DocumentsController).GetMethod(method)!.GetCustomAttributes(true).OfType<IAuthorizeData>());
            var policy = await AuthorizationPolicy.CombineAsync(provider.GetRequiredService<IAuthorizationPolicyProvider>(), metadata);
            Assert.Equal(allowed, (await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(actor, null, policy!)).Succeeded);
        }
    }

    [Theory]
    [InlineData("SuperAdmin", true)] [InlineData("CompanyAdmin", true)] [InlineData("SiteManager", true)]
    [InlineData("Supervisor", false)] [InlineData("Storekeeper", false)]
    public async Task Version_controller_roles_match_document_roles(string role, bool expected)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddAuthorization(o => o.AddPolicy(AppPolicies.ProcurementManagement, p => p.RequireRole(AppPolicies.ProcurementManagementRoles)));
        using var provider = services.BuildServiceProvider();
        var actor = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "test"));
        var metadata = typeof(DocumentVersionsController).GetCustomAttributes(true).OfType<IAuthorizeData>();
        var policy = await AuthorizationPolicy.CombineAsync(provider.GetRequiredService<IAuthorizationPolicyProvider>(), metadata);
        Assert.Equal(expected, (await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(actor, null, policy!)).Succeeded);
    }

    [Theory]
    [InlineData("You are not authorized to access this document.", 403)]
    [InlineData("Document was not found.", 404)]
    [InlineData("Document number is required.", 400)]
    public async Task Controller_preserves_access_missing_and_validation_semantics(string message, int expected)
    {
        var api = DispatchProxy.Create<CSM.Application.Compliance.Documents.IDocumentService, DomainApi>();
        ((DomainApi)(object)api).Message = message;
        var controller = new DocumentsController(api) { ControllerContext = new ControllerContext {
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
        Assert.DoesNotContain("No documents found", page.Markup);
        Assert.DoesNotContain("private diagnostic", page.Markup);
        Assert.DoesNotContain(page.FindAll("button"), b => b.TextContent.Trim() is "Add Document" or "Edit");
        Assert.Equal(status == 401, page.FindAll("#document-" + route + "-sign-in").Count == 1);
        Assert.Equal(status == 500, page.FindAll("#retry-document-" + route).Count == 1);
    }

    [Theory]
    [InlineData("list")] [InlineData("create")] [InlineData("edit")]
    public void Retry_recovers_protected_data_without_saving(string route)
    {
        using var context = Context(out var state);
        state.Status = HttpStatusCode.ServiceUnavailable;
        var page = Render(context, route);
        page.WaitForAssertion(() => Assert.Single(page.FindAll("#retry-document-" + route)));
        state.Status = null;
        page.Find("#retry-document-" + route).Click();
        page.WaitForAssertion(() => Assert.Contains(route == "create" ? "Document Number" : "DOC-TEST-1", page.Markup));
        Assert.Empty(page.FindAll("[role=alert]"));
    }

    [Theory]
    [InlineData("edit")]
    public void Missing_document_has_no_form_or_retry(string route)
    {
        using var context = Context(out var state);
        state.Missing = true;
        var page = Render(context, route);
        page.WaitForAssertion(() => Assert.Contains("Document was not found.", page.Markup));
        Assert.Empty(page.FindAll("form, input, select, textarea, #retry-document-" + route));
    }

    [Fact]
    public void Denied_version_history_hides_metadata_and_upload_controls()
    {
        using var context = Context(out var state);
        state.VersionsDenied = true;
        var page = Render(context, "edit");
        page.WaitForAssertion(() => Assert.Single(page.FindAll("[role=alert]")));
        Assert.Empty(page.FindAll("form, input, select, textarea, #retry-document-edit"));
        Assert.DoesNotContain("private diagnostic", page.Markup);
    }

    [Theory]
    [InlineData("SiteManager", false, false, false, false)]
    [InlineData("Supervisor", true, false, false, false)]
    [InlineData("CompanyAdmin", true, true, false, false)]
    [InlineData("SiteManager", true, false, true, false)]
    [InlineData("CompanyAdmin", true, false, false, true)]
    [InlineData("SuperAdmin", false, true, false, true)]
    public async Task Version_reads_reuse_document_role_company_and_deleted_guards(string role, bool companyAssigned, bool foreign, bool deleted, bool allowed)
    {
        using var db = new CSM.Infrastructure.Persistence.ApplicationDbContext(
            new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<CSM.Infrastructure.Persistence.ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var company = Guid.NewGuid(); var actorId = Guid.NewGuid(); var docId = Guid.NewGuid();
        db.Users.Add(new CSM.Domain.Entities.Identity.User { Id = actorId, IsActive = true, CompanyId = companyAssigned ? company : null,
            UserRoles = [new CSM.Domain.Entities.Identity.UserRole { Id = Guid.NewGuid(),
                Role = new CSM.Domain.Entities.Identity.Role { Id = Guid.NewGuid(), Name = role } }] });
        db.Documents.Add(new CSM.Domain.Entities.Compliance.Document { Id = docId, CompanyId = foreign ? Guid.NewGuid() : company,
            IsDeleted = deleted, DocumentNumber = "DOC-TEST-1", Name = "Test Document", FileName = "test.pdf", StoragePath = "/uploads/documents/test.pdf" });
        db.DocumentVersions.Add(new CSM.Domain.Entities.Compliance.DocumentVersion {
            Id = Guid.NewGuid(), DocumentId = docId, VersionNumber = 1, FileName = "authorized.pdf", StoragePath = "/uploads/documents/authorized.pdf" });
        db.SaveChanges(true);
        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddScoped<CSM.Application.Compliance.Documents.IDocumentService, CSM.Infrastructure.Services.DocumentService>();
        services.AddScoped<CSM.Infrastructure.Services.DocumentVersionService>();
        using var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<CSM.Infrastructure.Services.DocumentVersionService>();
        if (allowed)
            Assert.Equal("authorized.pdf", Assert.Single(await service.GetByDocumentAsync(actorId, docId, default)).FileName);
        else
            await Assert.ThrowsAsync<DocumentManagementException>(() => service.GetByDocumentAsync(actorId, docId, default));
    }

    [Theory]
    [InlineData(false, 401)] [InlineData(false, 403)] [InlineData(false, 500)]
    [InlineData(true, 401)] [InlineData(true, 403)] [InlineData(true, 500)]
    public async Task File_clients_preserve_status_without_response_body(bool upload, int status)
    {
        using var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddDataProtection();
        context.Services.AddScoped<ProtectedSessionStorage>();
        using var http = new HttpClient(new Response((HttpStatusCode)status)) { BaseAddress = new Uri("https://api.test/api/") };
        var service = new ComplianceService(new Factory(http), context.Services.GetRequiredService<ProtectedSessionStorage>());
        var error = await Assert.ThrowsAsync<HttpRequestException>(async () => {
            if (upload) await service.UploadDocumentVersionAsync(Guid.NewGuid(), new BrowserFile(), null);
            else await service.DownloadDocumentVersionAsync(Guid.NewGuid(), Guid.NewGuid());
        });
        Assert.Equal((HttpStatusCode)status, error.StatusCode);
        Assert.DoesNotContain("private diagnostic", error.Message);
    }

    private sealed class Factory(HttpClient http) : IHttpClientFactory { public HttpClient CreateClient(string name) => http; }
    private sealed class Response(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent("private diagnostic") });
    }
    private sealed class BrowserFile : IBrowserFile
    {
        public string Name => "test.txt";
        public DateTimeOffset LastModified => DateTimeOffset.UnixEpoch;
        public long Size => 3;
        public string ContentType => "text/plain";
        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) =>
            new MemoryStream(new byte[] { 1, 2, 3 });
    }

    private static IRenderedFragment Render(TestContext context, string route) => route switch {
        "list" => context.RenderComponent<Documents>(),
        "create" => context.RenderComponent<DocumentCreate>(),
        _ => context.RenderComponent<DocumentEdit>(p => p.Add(x => x.Id, Guid.Parse("11111111-1111-1111-1111-111111111111")))
    };
    private static TestContext Context(out State state)
    {
        var context = new TestContext(); state = new();
        var api = DispatchProxy.Create<IComplianceService, Api>(); ((Api)(object)api).State = state;
        context.Services.AddSingleton(api);
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        return context;
    }
    public sealed class State { public HttpStatusCode? Status; public bool Missing; public bool VersionsDenied; }
    public class Api : DispatchProxy
    {
        public State State = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            var row = new DocumentResponse { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                DocumentNumber = "DOC-TEST-1", Name = "Test Document", DocumentType = "General", FileName = "test.pdf", StoragePath = "/uploads/documents/test.pdf" };
            if (method!.Name == nameof(IComplianceService.GetDocumentsAsync))
                return State.Status.HasValue ? Task.FromException<IReadOnlyList<DocumentResponse>>(Error(State.Status.Value))
                    : Task.FromResult<IReadOnlyList<DocumentResponse>>(new[] { row });
            if (method.Name == nameof(IComplianceService.GetDocumentVersionsAsync))
                return State.VersionsDenied ? Task.FromException<IReadOnlyList<DocumentVersionResponse>>(Error(HttpStatusCode.Forbidden))
                    : Task.FromResult<IReadOnlyList<DocumentVersionResponse>>(Array.Empty<DocumentVersionResponse>());
            if (method.Name == nameof(IComplianceService.GetDocumentAsync))
                return State.Status.HasValue ? Task.FromException<DocumentResponse?>(Error(State.Status.Value))
                    : Task.FromResult<DocumentResponse?>(State.Missing ? null : row);
            throw new InvalidOperationException("Unexpected document mutation.");
        }
    }
    public class DomainApi : DispatchProxy
    {
        public string Message = "";
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            Task.FromException<CSM.Application.Compliance.Documents.Dtos.DocumentResponse>(new DocumentManagementException(Message));
    }
    private static HttpRequestException Error(HttpStatusCode status) => new("private diagnostic", null, status);
}
