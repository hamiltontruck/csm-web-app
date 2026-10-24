namespace CSM.Web.Models.UserPreferences;

public sealed record UpdateUserPreferencesRequest(
    string Theme,
    string Language,
    string TimeZone,
    string DateFormat,
    string TimeFormat,
    string NumberFormat,
    bool CompactMode,
    bool ConfirmActions,
    bool RememberNavigation,
    bool EmailNotificationsEnabled,
    bool InAppNotificationsEnabled,
    bool PushNotificationsEnabled,
    bool SecurityAlertsEnabled,
    bool ProjectNotificationsEnabled,
    bool ApprovalNotificationsEnabled,
    bool InventoryNotificationsEnabled,
    bool HRNotificationsEnabled,
    bool FinanceNotificationsEnabled,
    bool AnalyticsConsent,
    bool ProductImprovementConsent);