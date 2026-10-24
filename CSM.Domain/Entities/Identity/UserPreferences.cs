using CSM.Domain.Common;

namespace CSM.Domain.Entities.Identity;

public sealed class UserPreferences : AuditableEntity
{
    public Guid UserId { get; set; }

    public string Theme { get; set; } = "System";
    public string Language { get; set; } = "en";
    public string TimeZone { get; set; } = "UTC";
    public string DateFormat { get; set; } = "dd/MM/yyyy";
    public string TimeFormat { get; set; } = "24h";
    public string NumberFormat { get; set; } = "en-US";

    public bool CompactMode { get; set; }
    public bool ConfirmActions { get; set; } = true;
    public bool RememberNavigation { get; set; } = true;

    public bool EmailNotificationsEnabled { get; set; } = true;
    public bool InAppNotificationsEnabled { get; set; } = true;
    public bool PushNotificationsEnabled { get; set; } = true;
    public bool SecurityAlertsEnabled { get; set; } = true;

    public bool ProjectNotificationsEnabled { get; set; } = true;
    public bool ApprovalNotificationsEnabled { get; set; } = true;
    public bool InventoryNotificationsEnabled { get; set; } = true;
    public bool HRNotificationsEnabled { get; set; } = true;
    public bool FinanceNotificationsEnabled { get; set; } = true;

    public bool AnalyticsConsent { get; set; }
    public bool ProductImprovementConsent { get; set; }

    public User? User { get; set; }
}