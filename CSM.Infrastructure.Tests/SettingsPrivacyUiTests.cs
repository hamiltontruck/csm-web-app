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
    private static TestContext Context(Api handler, bool admin = false)
    {
        var context = new TestContext(); context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddDataProtection(); context.Services.AddScoped<ProtectedSessionStorage>();
        context.Services.AddLocalization();
        WorkspaceTestAccess.Add(context, admin ? ["CompanyAdmin"] : ["ProcurementOfficer"], admin ? ["Users_View"] : ["Procurement_Inventory_View"]);
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

    [Fact]
    public async Task Preference_controls_are_disabled_during_a_delayed_save()
    {
        var api = new Api { PreferenceSaveGate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        using var context = Context(api); var page = context.RenderComponent<Settings>();
        page.WaitForAssertion(() => Assert.Contains("All changes saved", page.Markup)); page.Find("#compactMode").Change(true);
        var save = page.FindAll("button").Single(x => x.TextContent.Trim() == "Save preferences").ClickAsync(new());
        try { page.WaitForAssertion(() => Assert.True(page.Find("fieldset.settings-panel-fields").HasAttribute("disabled"))); }
        finally { api.PreferenceSaveGate.TrySetResult(); await save; }
    }

    [Fact]
    public async Task Profile_controls_are_disabled_during_a_delayed_save()
    {
        var api = new Api { ProfileSaveGate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        using var context = Context(api); var page = context.RenderComponent<Settings>();
        page.WaitForAssertion(() => Assert.Contains("All changes saved", page.Markup)); Click(page, "Account"); Click(page, "Edit profile");
        page.Find("#profileFirstName").Change("Fresh"); var save = page.Find("form").SubmitAsync();
        try { page.WaitForAssertion(() => Assert.True(page.Find("fieldset.settings-profile-fields").HasAttribute("disabled"))); }
        finally { api.ProfileSaveGate.TrySetResult(); await save; }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Privacy_history_timeout_or_malformed_json_stays_in_the_panel(bool malformed)
    {
        var api = new Api { HistoryTimeout = !malformed, MalformedHistory = malformed };
        using var context = Context(api); var page = context.RenderComponent<Settings>();
        page.WaitForAssertion(() => Assert.Contains("All changes saved", page.Markup)); Click(page, "Data Management"); Click(page, "View history");
        page.WaitForAssertion(() => Assert.Contains("Privacy request history could not be loaded", page.Markup));
    }

    [Fact]
    public void Confirmed_submission_keeps_success_when_history_refresh_times_out()
    {
        var api = new Api { HistoryTimeout = true }; using var context = Context(api); var page = context.RenderComponent<Settings>();
        page.WaitForAssertion(() => Assert.Contains("All changes saved", page.Markup)); Click(page, "Data Management"); Click(page, "Submit request");
        page.WaitForAssertion(() => Assert.Contains("Your request was submitted. The history could not be refreshed", page.Markup));
        Assert.Equal(1, api.PrivacySubmissions);
    }

    [Fact]
    public void Password_timeout_shows_feedback_without_signing_out()
    {
        var api = new Api { PasswordTimeout = true }; using var context = Context(api); var page = context.RenderComponent<Settings>();
        page.WaitForAssertion(() => Assert.Contains("All changes saved", page.Markup)); Click(page, "Security"); Click(page, "Change password");
        page.Find("#currentPassword").Change("Current-password123!"); page.Find("#newPassword").Change("New-password123!");
        page.Find("#confirmNewPassword").Change("New-password123!"); Click(page, "Update password");
        page.WaitForAssertion(() => Assert.Contains("Password change could not be confirmed", page.Markup));
    }

    [Fact]
    public void Profile_save_then_refresh_shows_current_database_summary_without_new_login()
    {
        var api = new Api(); using var context = Context(api); var page = context.RenderComponent<Settings>();
        page.WaitForAssertion(() => Assert.Contains("All changes saved", page.Markup)); Click(page, "Account"); Click(page, "Edit profile");
        page.Find("#profileFirstName").Change("Fresh"); page.Find("form").Submit();
        page.WaitForAssertion(() => Assert.Contains("Profile updated successfully", page.Markup));
        page.FindAll("button").Single(x => x.TextContent.Contains("account information")).Click();
        page.WaitForAssertion(() => Assert.Contains("Fresh User", page.Find("dl").TextContent));
    }

    [Theory]
    [InlineData("en")] [InlineData("am")] [InlineData("om")]
    public void Notification_controls_have_localized_labels_and_truthful_persistence_copy(string language)
    {
        var previous = System.Globalization.CultureInfo.CurrentUICulture;
        try
        {
            System.Globalization.CultureInfo.CurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo(language);
            using var context = Context(new Api()); var page = context.RenderComponent<Settings>();
            page.WaitForAssertion(() => Assert.Contains("All changes saved", page.Markup)); Click(page, "Notifications");
            Assert.DoesNotContain("Settings.Notifications.", page.Markup);
            Assert.DoesNotContain("page state only", page.Markup); Assert.DoesNotContain("connected to persistent storage", page.Markup);
        }
        finally { System.Globalization.CultureInfo.CurrentUICulture = previous; }
    }

    [Fact]
    public void Localized_history_does_not_mislabel_local_times_as_UTC()
    {
        using var context = Context(new Api { History = true }); var page = context.RenderComponent<Settings>();
        page.WaitForAssertion(() => Assert.Contains("All changes saved", page.Markup)); Click(page, "Security");
        page.WaitForAssertion(() => Assert.Contains("11/11/2026 03:00", page.Markup));
        Assert.DoesNotContain("Date (UTC)", page.Markup); Assert.Contains("Africa/Addis_Ababa", page.Markup);
    }

    [Fact]
    public void Privacy_submission_timeout_is_unconfirmed_panel_feedback()
    {
        using var context = Context(new Api { SubmissionTimeout = true }); var page = context.RenderComponent<Settings>();
        page.WaitForAssertion(() => Assert.Contains("All changes saved", page.Markup)); Click(page, "Data Management"); Click(page, "Submit request");
        page.WaitForAssertion(() => Assert.Contains("Privacy request could not be confirmed", page.Markup));
    }

    [Fact]
    public void Display_and_notification_copy_describes_working_preview_and_persistence()
    {
        using var context = Context(new Api()); var page = context.RenderComponent<Settings>();
        page.WaitForAssertion(() => Assert.Contains("All changes saved", page.Markup)); Click(page, "Appearance");
        Assert.DoesNotContain("requires integration with the shared layout", page.Markup);
        Click(page, "Notifications"); Assert.DoesNotContain("page state only", page.Markup);
    }

    [Fact]
    public void Authorized_audit_history_uses_saved_date_time_and_zone()
    {
        using var context = Context(new Api { Audit = true }, admin: true); var page = context.RenderComponent<Settings>();
        page.WaitForAssertion(() => Assert.Contains("All changes saved", page.Markup)); Click(page, "Privacy Controls"); Click(page, "Load audit history");
        page.WaitForAssertion(() => Assert.Contains("11/11/2026 03:00", page.Markup));
        Assert.DoesNotContain("Date (UTC)", page.Markup);
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
        public TaskCompletionSource? PreferenceSaveGate;
        public TaskCompletionSource? ProfileSaveGate;
        public bool HistoryTimeout, MalformedHistory, PasswordTimeout, SubmissionTimeout, Audit;
        private MyProfileData Profile = new() { FirstName = "Own", LastName = "User", PhoneNumber = "123", JobTitle = "Officer" };
        public List<string> Reads = [];
        public List<UpdateUserPreferencesRequest> Writes = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath.Replace("/api/", "");
            if (request.Method == HttpMethod.Get) Reads.Add(path);
            if (path == "privacy-data-requests" && request.Method == HttpMethod.Post)
            {
                if (SubmissionTimeout) throw new TaskCanceledException("Local timeout fixture");
                PrivacySubmissions++;
                return new(HttpStatusCode.Created) { Content = JsonContent.Create(new { id = Guid.NewGuid(), status = "Submitted" }) };
            }
            if (path == "privacy-data-requests/mine")
            {
                if (HistoryTimeout) throw new TaskCanceledException("Local timeout fixture");
                if (MalformedHistory) return new(HttpStatusCode.OK) { Content = new StringContent("{broken json", System.Text.Encoding.UTF8, "application/json") };
                return new(HttpStatusCode.ServiceUnavailable);
            }
            if (path == "auth/change-password" && PasswordTimeout) throw new TaskCanceledException("Local timeout fixture");
            if (path == "audit" && Audit) return new(HttpStatusCode.OK) { Content = JsonContent.Create(new[] {
                new AuditHistoryEntry { Id = Guid.NewGuid(), CreatedAtUtc = new DateTime(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc), Action = "Local audit fixture" }
            }) };
            if (path == "auth/me/profile")
            {
                if (request.Method == HttpMethod.Put)
                {
                    Profile = (await request.Content!.ReadFromJsonAsync<MyProfileData>(ct))!;
                    if (ProfileSaveGate is not null) await ProfileSaveGate.Task;
                }
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { Profile.FirstName, Profile.LastName, Profile.PhoneNumber,
                    Profile.JobTitle, Name = Profile.FirstName + " " + Profile.LastName, email = "own@example.test",
                    companyId = Companyless ? null : "11111111-1111-1111-1111-111111111111", roles = new[] { "ProcurementOfficer" } }) };
            }
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
            if (request.Method == HttpMethod.Put) { value = (await request.Content!.ReadFromJsonAsync<UpdateUserPreferencesRequest>(ct))!; Writes.Add(value); if (PreferenceSaveGate is not null) await PreferenceSaveGate.Task; if (FailWrite) return new(HttpStatusCode.ServiceUnavailable); }
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new UserPreferencesResponse(Guid.NewGuid(), value.Theme, value.Language,
                value.TimeZone, value.DateFormat, value.TimeFormat, value.NumberFormat, value.CompactMode, value.ConfirmActions, value.RememberNavigation,
                value.EmailNotificationsEnabled, value.InAppNotificationsEnabled, value.PushNotificationsEnabled, value.SecurityAlertsEnabled,
                value.ProjectNotificationsEnabled, value.ApprovalNotificationsEnabled, value.InventoryNotificationsEnabled, value.HRNotificationsEnabled,
                value.FinanceNotificationsEnabled, value.AnalyticsConsent, value.ProductImprovementConsent)) };
        }
    }
}
