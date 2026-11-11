using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using Bunit;
using CSM.Web.Components.Pages;
using CSM.Web.Models.UserPreferences;
using CSM.Web.Services;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace CSM.Infrastructure.Tests;

public sealed class SettingsPrivacyUiTests
{
    private static TestContext Context(Api handler)
    {
        var context = new TestContext(); context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddDataProtection(); context.Services.AddScoped<ProtectedSessionStorage>();
        context.Services.AddLocalization();
        WorkspaceTestAccess.Add(context, ["ProcurementOfficer"], ["Procurement_Inventory_View"]);
        context.Services.AddSingleton(DispatchProxy.Create<IAuthService, Auth>());
        context.Services.AddSingleton<IHttpClientFactory>(new Factory(handler));
        context.Services.AddScoped<UserPreferencesService>(); context.Services.AddScoped<UserSessionsService>();
        context.Services.AddScoped<LoginHistoryService>(); context.Services.AddScoped<AuditHistoryService>();
        context.Services.AddScoped<PersonalDataExportService>(); context.Services.AddScoped<PrivacyDataRequestService>();
        context.Services.AddScoped<PasswordChangeService>(); context.Services.AddScoped<CurrentUserProfileService>();
        context.Services.AddScoped<OrganizationMembershipService>(); context.Services.AddScoped<MyProfileService>();
        return context;
    }
    private static void Click(IRenderedComponent<Settings> page, string label) =>
        page.FindAll("button").Single(x => x.TextContent.Trim() == label).Click();

    [Fact]
    public void Preferences_have_eight_categories_and_do_not_load_admin_or_account_endpoints_initially()
    {
        var api = new Api(); using var context = Context(api); var page = context.RenderComponent<Settings>();
        page.WaitForAssertion(() => Assert.Equal(8, page.FindAll(".settings-sidebar button").Count));
        Assert.Contains("Save preferences", page.Markup);
        Assert.Contains("Compact interface", page.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Settings.General.", page.Markup);
        Assert.All(api.Reads, path => Assert.Equal("user/preferences", path));
    }

    [Fact]
    public void Save_roundtrips_all_values_and_discard_does_not_write()
    {
        var api = new Api(); using var context = Context(api); var page = context.RenderComponent<Settings>();
        page.WaitForAssertion(() => Assert.Contains("All changes saved", page.Markup));
        page.Find("#compactMode").Change(true); Click(page, "Save preferences");
        page.WaitForAssertion(() => Assert.Single(api.Writes));
        Assert.True(api.Writes[0].CompactMode); Assert.Equal("Africa/Addis_Ababa", api.Writes[0].TimeZone);
        page.Find("#compactMode").Change(false); Click(page, "Discard changes");
        Assert.Single(api.Writes); Assert.True(page.Find("#compactMode").HasAttribute("checked"));
    }

    [Fact]
    public void Failed_load_is_unavailable_and_failed_save_preserves_edits()
    {
        var api = new Api { FailRead = true }; using var context = Context(api); var page = context.RenderComponent<Settings>();
        page.WaitForAssertion(() => Assert.Contains("Preferences unavailable", page.Markup));
        Assert.DoesNotContain("All changes saved", page.Markup);
    }

    [Fact]
    public void Failed_save_keeps_draft_and_restore_defaults_requires_explicit_save()
    {
        var api = new Api { FailWrite = true }; using var context = Context(api); var page = context.RenderComponent<Settings>();
        page.WaitForAssertion(() => Assert.Contains("All changes saved", page.Markup));
        page.Find("#compactMode").Change(true); Click(page, "Save preferences");
        page.WaitForAssertion(() => Assert.Contains("Could not save preferences", page.Markup));
        Assert.True(page.Find("#compactMode").HasAttribute("checked")); Assert.Contains("Unsaved changes", page.Markup);
        Click(page, "Restore defaults"); Assert.Single(api.Writes); Assert.Contains("Select Save", page.Markup);
    }

    [Fact]
    public void Password_panel_keeps_complex_password_policy_and_reader_does_not_query_company_audit()
    {
        var api = new Api(); using var context = Context(api); var page = context.RenderComponent<Settings>();
        page.WaitForAssertion(() => Assert.Contains("All changes saved", page.Markup));
        Click(page, "Security"); Click(page, "Change password");
        Assert.False(page.Find("#newPassword").HasAttribute("maxlength"));
        page.Find("#currentPassword").Change("Current-password123!");
        page.Find("#newPassword").Change("123456"); page.Find("#confirmNewPassword").Change("123456");
        Click(page, "Update password");
        Assert.Contains("12", page.Markup); Assert.DoesNotContain(api.Reads, x => x.Contains("audit"));
        Click(page, "Privacy Controls"); Assert.DoesNotContain(api.Reads, x => x.Contains("audit"));
    }

    [Fact]
    public void History_uses_saved_date_time_and_time_zone_preferences()
    {
        var api = new Api { History = true }; using var context = Context(api); var page = context.RenderComponent<Settings>();
        page.WaitForAssertion(() => Assert.Contains("All changes saved", page.Markup)); Click(page, "Security");
        page.WaitForAssertion(() => Assert.Contains("11/11/2026 03:00", page.Markup));
    }

    [Fact]
    public void Missing_site_access_summary_does_not_claim_the_user_has_no_site_access()
    {
        var api = new Api(); using var context = Context(api); var page = context.RenderComponent<Settings>();
        page.WaitForAssertion(() => Assert.Contains("All changes saved", page.Markup)); Click(page, "Account");
        page.WaitForAssertion(() => Assert.Contains("Load organization membership below", page.Markup));
    }

    [Fact]
    public void Companyless_account_can_download_own_data_but_cannot_submit_company_review_requests()
    {
        var api = new Api { Companyless = true }; using var context = Context(api); var page = context.RenderComponent<Settings>();
        page.WaitForAssertion(() => Assert.Contains("All changes saved", page.Markup)); Click(page, "Data Management");
        page.WaitForAssertion(() => Assert.True(page.Find("#privacyRequestType").HasAttribute("disabled")));
        Assert.False(page.FindAll("button").Single(x => x.TextContent.Contains("Export my data")).HasAttribute("disabled"));
        Assert.DoesNotContain(api.Reads, x => x.Contains("privacy-data-requests") || x.Contains("audit"));
    }

    [Fact]
    public void Successful_privacy_submission_is_not_reported_failed_when_history_refresh_is_unavailable()
    {
        var api = new Api(); using var context = Context(api); var page = context.RenderComponent<Settings>();
        page.WaitForAssertion(() => Assert.Contains("All changes saved", page.Markup)); Click(page, "Data Management");
        Assert.DoesNotContain(api.Reads, path => path.Contains("privacy-data-requests"));
        page.Find("#privacyRequestDescription").Change("Please review my data."); Click(page, "Submit request");
        page.WaitForAssertion(() => Assert.Contains("Your request was submitted. The history could not be refreshed", page.Markup));
        Assert.Equal(1, api.PrivacySubmissions);
        Assert.DoesNotContain("Your privacy request could not be submitted", page.Markup);
    }

    private sealed class Factory(Api api) : IHttpClientFactory
    { public HttpClient CreateClient(string name) => new(api, false) { BaseAddress = new Uri("https://test/api/") }; }
    public class Auth : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch
        {
            nameof(IAuthService.ApplyAuthenticationAsync) or nameof(IAuthService.LogoutAsync) => Task.CompletedTask,
            nameof(IAuthService.GetRolesAsync) or nameof(IAuthService.GetPermissionsAsync) => Task.FromResult<IReadOnlyCollection<string>>([]),
            nameof(IAuthService.GetTokenAsync) => Task.FromResult<string?>(null),
            _ => throw new NotSupportedException(method.Name)
        };
    }
    private sealed class Api : HttpMessageHandler
    {
        public bool FailRead;
        public bool FailWrite;
        public bool History;
        public bool Companyless;
        public int PrivacySubmissions;
        public List<string> Reads = [];
        public List<UpdateUserPreferencesRequest> Writes = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath.Replace("/api/", "");
            if (request.Method == HttpMethod.Get) Reads.Add(path);
            if (path == "privacy-data-requests" && request.Method == HttpMethod.Post)
            {
                PrivacySubmissions++;
                return new(HttpStatusCode.Created) { Content = JsonContent.Create(new { id = Guid.NewGuid(), status = "Submitted" }) };
            }
            if (path == "privacy-data-requests/mine") return new(HttpStatusCode.ServiceUnavailable);
            if (path == "auth/me") return new(HttpStatusCode.OK) { Content = JsonContent.Create(new {
                name = "Own User", email = "own@example.test", companyId = Companyless ? null : "11111111-1111-1111-1111-111111111111", roles = new[] { "ProcurementOfficer" }
            }) };
            if (path == "auth/login-history" && History) return new(HttpStatusCode.OK) { Content = JsonContent.Create(new[] {
                new LoginHistoryItem { Id = Guid.NewGuid(), OccurredAtUtc = new DateTime(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc), Succeeded = true }
            }) };
            if (path != "user/preferences") return new(HttpStatusCode.Forbidden);
            if (FailRead) return new(HttpStatusCode.ServiceUnavailable);
            var value = new UpdateUserPreferencesRequest("System", "en", "Africa/Addis_Ababa", "dd/MM/yyyy", "24h", "en-US", false,
                true, true, true, true, true, true, true, true, true, true, true, false, false);
            if (request.Method == HttpMethod.Put) { value = (await request.Content!.ReadFromJsonAsync<UpdateUserPreferencesRequest>(ct))!; Writes.Add(value); if (FailWrite) return new(HttpStatusCode.ServiceUnavailable); }
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new UserPreferencesResponse(Guid.NewGuid(), value.Theme, value.Language,
                value.TimeZone, value.DateFormat, value.TimeFormat, value.NumberFormat, value.CompactMode, value.ConfirmActions, value.RememberNavigation,
                value.EmailNotificationsEnabled, value.InAppNotificationsEnabled, value.PushNotificationsEnabled, value.SecurityAlertsEnabled,
                value.ProjectNotificationsEnabled, value.ApprovalNotificationsEnabled, value.InventoryNotificationsEnabled, value.HRNotificationsEnabled,
                value.FinanceNotificationsEnabled, value.AnalyticsConsent, value.ProductImprovementConsent)) };
        }
    }
}
