using CSM.Domain.Enums;

namespace CSM.Application.Notifications;

/// <summary>
/// Used by other services (expenses, leave, attendance ...) to raise
/// notifications. Every method only STAGES rows on the current DbContext.
/// The calling service saves, so the notification and the change that
/// caused it are committed together or not at all.
/// </summary>
public interface INotificationPublisher
{
    void StageForUser(
        Guid companyId,
        Guid recipientUserId,
        string title,
        string message,
        NotificationSeverity severity,
        string sourceEntityType,
        Guid sourceEntityId);

    /// <summary>
    /// Records that belong to a site. Notifies every active user in the
    /// company who holds the permission and could actually open the record:
    /// company-wide roles, users with no site assignments, or users assigned
    /// to that site. For a record with no site, Site Managers are skipped.
    /// </summary>
    Task StageForPermissionHoldersAsync(
        Guid companyId,
        Guid? siteId,
        GranularPermission permission,
        Guid? excludeUserId,
        string title,
        string message,
        NotificationSeverity severity,
        string sourceEntityType,
        Guid sourceEntityId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that are not tied to a site (for example leave requests).
    /// Notifies every active user in the company who holds the permission.
    /// </summary>
    Task StageForCompanyPermissionHoldersAsync(
        Guid companyId,
        GranularPermission permission,
        Guid? excludeUserId,
        string title,
        string message,
        NotificationSeverity severity,
        string sourceEntityType,
        Guid sourceEntityId,
        CancellationToken cancellationToken = default);
}
