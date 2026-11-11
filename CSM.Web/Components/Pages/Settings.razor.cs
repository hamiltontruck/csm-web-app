using CSM.Web.Services;
using CSM.Web.Services.Access;
using CSM.Web.Models.Security;
using CSM.Web.Models.UserPreferences;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
namespace CSM.Web.Components.Pages;
public partial class Settings
{

    [CascadingParameter] public WorkspaceAccess? WorkspaceAccess { get; set; }
    private bool CanViewAudit => WorkspaceAccess?.CanVisit("/users") == true;
    private bool HasCompany => Guid.TryParse(_currentUserProfile?.CompanyId, out var companyId) && companyId != Guid.Empty;

    [Inject]
    private UserPreferencesService PreferencesService { get; set; } = default!;

    [Inject]
    private IJSRuntime JS { get; set; } = default!;

    private string _activeTab = "general";
    private string _theme = "system";
    private string _language = "en";
    private string _dateFormat = "dd/MM/yyyy";
    private string _timeFormat = "24";
    private string _timeZone = "UTC";
    private string _numberFormat = "en-US";

    private bool _compactMode;
    private bool _confirmActions = true;
    private bool _rememberNavigation = true;

    private bool _projectNotifications = true;
    private bool _approvalNotifications = true;
    private bool _inventoryNotifications = true;
    private bool _hrNotifications = true;
    private bool _financialNotifications = true;
    private bool _inAppNotifications = true;
    private bool _emailNotifications = true;

    private bool _pushNotifications = true;
    private bool _securityAlerts = true;
    private bool _analyticsConsent;
    private bool _productImprovementConsent;

    private List<UserSessionModel> _sessions = [];
    private bool _sessionsLoading;
    private string? _sessionsError;
    private Guid? _revokingSessionId;

    private List<LoginHistoryItem> _loginHistory = [];
    private bool _loginHistoryLoading;
    private bool _loginHistoryLoaded;
    private string? _loginHistoryError;

    private bool _revokingOtherSessions;
    private string? _revokeOtherSessionsMessage;
    private bool _revokeOtherSessionsSucceeded;

    private List<AuditHistoryEntry> _auditLogs = [];
    private bool _auditLoading;
    private bool _auditLoaded;
    private string? _auditError;

    private bool _dataExportLoading;
    private string? _dataExportError;
    private string? _dataExportMessage;

    private List<PrivacyDataRequestItem> _privacyDataRequests = [];
    private bool _privacyRequestsLoading;
    private string? _privacyRequestsError;
    private bool _privacyRequestsLoaded;

    private CurrentUserProfile? _currentUserProfile;
    private bool _currentUserProfileLoading;
    private bool _currentUserProfileLoaded;
    private string? _currentUserProfileError;

    private List<OrganizationMembershipItem> _organizationMemberships = [];
    private bool _organizationMembershipLoading;
    private bool _organizationMembershipLoaded;
    private string? _organizationMembershipError;

    private bool _culturePersisted;
    private bool _isLoading;
    private bool _isSaving;
    private bool _preferencesLoaded;
    private UpdateUserPreferencesRequest? _savedPreferences;
    private string _message = string.Empty;
    private bool _messageIsError;

    private bool HasUnsavedChanges =>
        _preferencesLoaded &&
        _savedPreferences is not null &&
        BuildPreferencesRequest() != _savedPreferences;

    private string SettingsStatusText => !_preferencesLoaded
        ? "Preferences unavailable"
        : HasUnsavedChanges
            ? "Unsaved changes"
            : "All changes saved";

    private string SettingsStatusClass => !_preferencesLoaded
        ? "settings-status-unavailable"
        : HasUnsavedChanges
            ? "settings-status-unsaved"
            : "settings-status-saved";

    protected override Task OnInitializedAsync() =>
        LoadPreferencesAsync();

    private async Task LoadPreferencesAsync()
    {
        _isLoading = true;
        ClearMessage();

        try
        {
            var preferences = await PreferencesService.GetAsync();
            ApplyPreferences(preferences);
            _savedPreferences = BuildPreferencesRequest();
            _preferencesLoaded = true;

            try
            {
                _culturePersisted = await JS.InvokeAsync<bool>("csmCulture.set", _language);
                await JS.InvokeVoidAsync("csmTheme.persist", _theme);
                await JS.InvokeVoidAsync("csmTheme.persistCompact", _compactMode);
            }
            catch (JSException ex)
            {
                Logger.LogWarning(
                    ex,
                    "Preferences loaded, but browser settings could not be applied.");
                _message =
                    "Preferences loaded, but some display settings may require a refresh.";
                return;
            }

            var currentLanguage =
                System.Globalization.CultureInfo.CurrentUICulture
                    .TwoLetterISOLanguageName;
            if (_culturePersisted && !string.Equals(
                    currentLanguage,
                    _language,
                    StringComparison.OrdinalIgnoreCase))
            {
                Navigation.NavigateTo(Navigation.Uri, forceLoad: true);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load user preferences.");
            _messageIsError = true;
            _message = "Could not load preferences. Please try again.";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task LoadSessionsAsync()
    {
        _sessionsLoading = true;
        _sessionsError = null;

        try
        {
            var sessions = await SessionsService.GetSessionsAsync();

            _sessions = sessions.ToList();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load active sessions.");
            _sessionsError = "Unable to load sessions. Please try again.";
        }
        finally
        {
            _sessionsLoading = false;
        }
    }

    private async Task LoadLoginHistoryAsync()
    {
        _loginHistoryLoading = true;
        _loginHistoryError = null;

        try
        {
            var history = await LoginHistory.GetHistoryAsync();

            _loginHistory = history.ToList();
            _loginHistoryLoaded = true;
        }
        catch (HttpRequestException ex)
        {
            _loginHistoryError = ex.StatusCode switch
            {
                System.Net.HttpStatusCode.Unauthorized =>
                    "Your session has expired. Please sign in again.",

                System.Net.HttpStatusCode.Forbidden =>
                    "You are not authorized to view login history.",

                _ =>
                    "Unable to load login history. Please try again."
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load login history.");
            _loginHistoryError =
                "Unable to load login history. Please try again.";
        }
        finally
        {
            _loginHistoryLoading = false;
        }
    }

    private async Task SetThemeAsync(string theme)
    {
        _theme = theme;

        try
        {
            await JS.InvokeVoidAsync("csmTheme.apply", _theme);
        }
        catch (JSDisconnectedException)
        {
            // The browser circuit disconnected before the theme could be applied.
        }
    }

    private async Task RevokeSessionAsync(Guid sessionId)
    {
        var session = _sessions.FirstOrDefault(item => item.Id == sessionId);

        if (session is null || session.IsCurrent)
        {
            return;
        }

        if (_confirmActions &&
            !await JS.InvokeAsync<bool>(
                "confirm",
                $"Revoke the session for {session.DeviceName ?? "this device"}?"))
        {
            return;
        }

        _revokingSessionId = sessionId;
        _sessionsError = null;

        try
        {
            await SessionsService.RevokeSessionAsync(sessionId);
            await LoadSessionsAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to revoke session {SessionId}.", sessionId);
            _sessionsError =
                "Unable to revoke the selected session. Please try again.";
        }
        finally
        {
            _revokingSessionId = null;
        }
    }


private async Task RevokeOtherSessionsAsync()
{
    if (_confirmActions &&
        !await JS.InvokeAsync<bool>(
            "confirm",
            "Revoke renewal for all other devices? Existing access can last until the current access token expires."))
    {
        return;
    }

    _revokingOtherSessions = true;
    _revokeOtherSessionsMessage = null;

    try
    {
        var revokedCount =
            await SessionsService.RevokeOtherSessionsAsync();

        _revokeOtherSessionsSucceeded = true;
        _revokeOtherSessionsMessage =
            revokedCount == 0
                ? "No other active sessions were found."
                : $"Revoked renewal for {revokedCount} other device session(s). Existing access can last until token expiry.";

        await LoadSessionsAsync();
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "Failed to revoke other active sessions.");
        _revokeOtherSessionsSucceeded = false;
        _revokeOtherSessionsMessage =
            "Unable to revoke other sessions. Please try again.";
    }
    finally
    {
        _revokingOtherSessions = false;
    }
}

private async Task LoadAuditLogsAsync()
{
    if (!CanViewAudit) return;
    _auditLoading = true;
    _auditError = null;

    try
    {
        var logs = await AuditService.GetLogsAsync();
        _auditLogs = logs.ToList();
        _auditLoaded = true;
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "Failed to load audit history.");
        _auditError =
            "Unable to load audit history. Check your permissions and API connection.";
    }
    finally
    {
        _auditLoading = false;
    }
}

private async Task RequestPersonalDataExportAsync()
{
    _dataExportLoading = true;
    _dataExportError = null;
    _dataExportMessage = null;

    try
    {
        var result = await PersonalDataExport.ExportAsync();

        await JS.InvokeVoidAsync(
            "downloadFileFromBytes",
            result.FileName,
            "application/json",
            Convert.ToBase64String(result.Content));

        _dataExportMessage = "Your personal data export has been downloaded.";
    }
    catch (HttpRequestException ex)
    {
        _dataExportError = ex.StatusCode switch
        {
            System.Net.HttpStatusCode.Forbidden =>
                "You are not authorized to export personal data.",
            System.Net.HttpStatusCode.Unauthorized =>
                "Your session has expired. Sign in again.",
            _ => "The export could not be completed. Please try again."
        };
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "Failed to export personal data.");
        _dataExportError = "The export could not be completed. Please try again.";
    }
    finally
    {
        _dataExportLoading = false;
    }
}
private async Task LoadPrivacyDataRequestsAsync()
{
    if (!HasCompany) { _privacyRequestsError = "A company membership is required to submit or view privacy review requests."; return; }
    if (_privacyRequestsLoading || _privacyRequestsLoaded)
    {
        return;
    }

    _privacyRequestsLoading = true;
    _privacyRequestsError = null;

    try
    {
        _privacyDataRequests =
            await PrivacyDataRequests.GetMineAsync();

        _privacyRequestsLoaded = true;
    }
    catch (HttpRequestException)
    {
        _privacyRequestsError =
            "Privacy request history could not be loaded. Please try again.";
    }
    catch (Exception ex)
    {
        Logger.LogWarning(ex, "Privacy history response could not be processed.");
        _privacyRequestsError = "Privacy request history could not be loaded. Please try again.";
    }
    finally
    {
        _privacyRequestsLoading = false;
    }
}

private async Task RetryPrivacyDataRequestsAsync()
    {
        _privacyRequestsLoaded = false;
        await LoadPrivacyDataRequestsAsync();
    }



    private async Task ApplyCompactModeAsync()
    {
        await JS.InvokeVoidAsync("csmTheme.setCompact", _compactMode);
    }

private string _privacyRequestType = "DataAccess";
private string? _privacyRequestDescription;
private bool _privacyRequestSubmitting;
private string? _privacyRequestSubmitError;
private string? _privacyRequestSubmitSuccess;

private async Task SubmitPrivacyDataRequestAsync()
{
    if (!HasCompany) { _privacyRequestSubmitError = "A company membership is required to submit privacy review requests."; return; }
    if (_privacyRequestSubmitting)
    {
        return;
    }

    _privacyRequestSubmitting = true;
    _privacyRequestSubmitError = null;
    _privacyRequestSubmitSuccess = null;

    try
    {
        await PrivacyDataRequests.SubmitAsync(
            _privacyRequestType,
            _privacyRequestDescription);

        _privacyRequestDescription = null;

        // Reload the existing history after a successful submission.
        _privacyRequestsLoaded = false;
        await LoadPrivacyDataRequestsAsync();

        if (_privacyRequestsError is null)
        {
            _privacyRequestSubmitSuccess =
                "Your privacy request was submitted successfully.";
        }
        else
        {
            _privacyRequestSubmitSuccess =
                "Your request was submitted. The history could not be refreshed; use Retry to reload it.";
        }
    }
    catch (HttpRequestException)
    {
        _privacyRequestSubmitError =
            "Your privacy request could not be submitted. Please try again.";
    }
    catch (OperationCanceledException)
    {
        _privacyRequestSubmitError = "Privacy request could not be confirmed. Check history before trying again.";
    }
    catch (Exception ex)
    {
        Logger.LogWarning(ex, "Privacy request response could not be processed.");
        _privacyRequestSubmitError = "Privacy request could not be confirmed. Check history before trying again.";
    }
    finally
    {
        _privacyRequestSubmitting = false;
    }
}




    private string TabClass(string tab) =>
        _activeTab == tab ? "active" : string.Empty;

    private async Task SelectTab(string tab)
    {
        _activeTab = tab;

        if (tab is "privacy" or "data" && !_currentUserProfileLoaded) await LoadCurrentUserProfileAsync();
        if (tab == "sessions")
        {
            await LoadSessionsAsync();
        }
        else if (tab == "account" && !_currentUserProfileLoaded)
        {
            await LoadCurrentUserProfileAsync();
        }
        else if (tab == "security" && !_loginHistoryLoaded)
        {
            await LoadLoginHistoryAsync();
        }
    }

    private void ClearMessage()
    {
        _message = string.Empty;
        _messageIsError = false;
    }

    private async Task SavePreferencesAsync()
    {
        if (_isSaving || !_preferencesLoaded || !HasUnsavedChanges)
        {
            return;
        }

        _isSaving = true;
        ClearMessage();

        try
        {
            var oldLanguage = _savedPreferences!.Language;
            var savedPreferences = await PreferencesService.UpdateAsync(
                BuildPreferencesRequest());

            ApplyPreferences(savedPreferences);
            _savedPreferences = BuildPreferencesRequest();
            _messageIsError = false;
            _message = "Preferences saved successfully.";

            try
            {
                _culturePersisted = await JS.InvokeAsync<bool>("csmCulture.set", savedPreferences.Language);
                await JS.InvokeVoidAsync("csmTheme.persist", _theme);
                await JS.InvokeVoidAsync("csmTheme.persistCompact", _compactMode);
            }
            catch (JSException ex)
            {
                Logger.LogWarning(
                    ex,
                    "Preferences were saved, but browser settings could not be applied.");
                _message =
                    "Preferences were saved, but some display changes will apply after you refresh.";
                return;
            }

            if (_culturePersisted && !string.Equals(
                    oldLanguage,
                    savedPreferences.Language,
                    StringComparison.OrdinalIgnoreCase))
            {
                Navigation.NavigateTo(Navigation.Uri, forceLoad: true);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to save user preferences.");
            _messageIsError = true;
            _message = "Could not save preferences. Please try again.";
        }
        finally
        {
            _isSaving = false;
        }
    }

    private UpdateUserPreferencesRequest BuildPreferencesRequest() =>
        new(
            Theme: _theme.Equals("dark", StringComparison.OrdinalIgnoreCase)
                ? "Dark"
                : _theme.Equals("system", StringComparison.OrdinalIgnoreCase)
                    ? "System"
                    : "Light",
            Language: _language,
            TimeZone: _timeZone,
            DateFormat: _dateFormat,
            TimeFormat: _timeFormat == "12" ? "12h" : "24h",
            NumberFormat: _numberFormat,
            CompactMode: _compactMode,
            ConfirmActions: _confirmActions,
            RememberNavigation: _rememberNavigation,
            EmailNotificationsEnabled: _emailNotifications,
            InAppNotificationsEnabled: _inAppNotifications,
            PushNotificationsEnabled: _pushNotifications,
            SecurityAlertsEnabled: _securityAlerts,
            ProjectNotificationsEnabled: _projectNotifications,
            ApprovalNotificationsEnabled: _approvalNotifications,
            InventoryNotificationsEnabled: _inventoryNotifications,
            HRNotificationsEnabled: _hrNotifications,
            FinanceNotificationsEnabled: _financialNotifications,
            AnalyticsConsent: _analyticsConsent,
            ProductImprovementConsent: _productImprovementConsent);

    private void ApplyPreferences(UserPreferencesResponse preferences)
    {
        ApplyPreferences(
            new UpdateUserPreferencesRequest(
                preferences.Theme,
                preferences.Language,
                preferences.TimeZone,
                preferences.DateFormat,
                preferences.TimeFormat,
                preferences.NumberFormat,
                preferences.CompactMode,
                preferences.ConfirmActions,
                preferences.RememberNavigation,
                preferences.EmailNotificationsEnabled,
                preferences.InAppNotificationsEnabled,
                preferences.PushNotificationsEnabled,
                preferences.SecurityAlertsEnabled,
                preferences.ProjectNotificationsEnabled,
                preferences.ApprovalNotificationsEnabled,
                preferences.InventoryNotificationsEnabled,
                preferences.HRNotificationsEnabled,
                preferences.FinanceNotificationsEnabled,
                preferences.AnalyticsConsent,
                preferences.ProductImprovementConsent));
    }

    private void ApplyPreferences(UpdateUserPreferencesRequest preferences)
    {
        _theme = preferences.Theme.ToLowerInvariant() switch
        {
            "dark" => "dark",
            "system" => "system",
            _ => "light"
        };
        _language = preferences.Language;
        _timeZone = preferences.TimeZone;
        _dateFormat = preferences.DateFormat;
        _timeFormat = preferences.TimeFormat.StartsWith(
            "12",
            StringComparison.OrdinalIgnoreCase)
            ? "12"
            : "24";
        _numberFormat = preferences.NumberFormat;

        _compactMode = preferences.CompactMode;
        _confirmActions = preferences.ConfirmActions;
        _rememberNavigation = preferences.RememberNavigation;

        _projectNotifications = preferences.ProjectNotificationsEnabled;
        _approvalNotifications = preferences.ApprovalNotificationsEnabled;
        _inventoryNotifications = preferences.InventoryNotificationsEnabled;
        _hrNotifications = preferences.HRNotificationsEnabled;
        _financialNotifications = preferences.FinanceNotificationsEnabled;

        _inAppNotifications = preferences.InAppNotificationsEnabled;
        _emailNotifications = preferences.EmailNotificationsEnabled;
        _pushNotifications = preferences.PushNotificationsEnabled;
        _securityAlerts = preferences.SecurityAlertsEnabled;

        _analyticsConsent = preferences.AnalyticsConsent;
        _productImprovementConsent = preferences.ProductImprovementConsent;
    }

    private async Task DiscardPreferenceChangesAsync()
    {
        var savedPreferences = _savedPreferences;

        if (savedPreferences is null)
        {
            return;
        }

        ApplyPreferences(savedPreferences);
        _savedPreferences = BuildPreferencesRequest();
        ClearMessage();

        await JS.InvokeVoidAsync("csmTheme.persist", _theme);
        await JS.InvokeVoidAsync("csmTheme.persistCompact", _compactMode);
    }

    private async Task ResetPreferencesAsync()
    {
        _theme = "system";
        _language = "en";
        _dateFormat = "dd/MM/yyyy";
        _timeFormat = "24";
        _timeZone = "UTC";
        _numberFormat = "en-US";

        _compactMode = false;
        _confirmActions = true;
        _rememberNavigation = true;

        _projectNotifications = true;
        _approvalNotifications = true;
        _inventoryNotifications = true;
        _hrNotifications = true;
        _financialNotifications = true;

        _inAppNotifications = true;
        _emailNotifications = true;
        _pushNotifications = true;
        _securityAlerts = true;

        _analyticsConsent = false;
        _productImprovementConsent = false;

        _messageIsError = false;
        _message = "Defaults restored on this page. Select Save to persist them.";

        await JS.InvokeVoidAsync("csmTheme.apply", _theme);
        await JS.InvokeVoidAsync("csmTheme.setCompact", _compactMode);
    }



    private string FormatTimestamp(DateTime? value) => UserPreferenceFormatter.Timestamp(value,
        _savedPreferences?.DateFormat, _savedPreferences?.TimeFormat, _savedPreferences?.TimeZone, _savedPreferences?.Language);
}
