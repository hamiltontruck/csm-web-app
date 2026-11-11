using CSM.Application.Common.Exceptions;
using CSM.Application.UserPreferences;
using CSM.Application.UserPreferences.Dtos;
using CSM.Domain.Entities.Identity;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using DomainUserPreferences =
    CSM.Domain.Entities.Identity.UserPreferences;

namespace CSM.Infrastructure.Services;

public sealed class UserPreferencesService : IUserPreferencesService
{
    private readonly ApplicationDbContext _dbContext;

    public UserPreferencesService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<UserPreferencesResponse> GetAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        await EnsureActiveUserAsync(
            currentUserId,
            cancellationToken);

        var preferences = await _dbContext.Set<DomainUserPreferences>()
            .SingleOrDefaultAsync(
                x => x.UserId == currentUserId && !x.IsDeleted,
                cancellationToken);

        if (preferences is null)
        {
            preferences = new DomainUserPreferences
            {
                UserId = currentUserId,
                CreatedBy = currentUserId
            };

            _dbContext.Set<DomainUserPreferences>().Add(preferences);

            try { await _dbContext.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException
                { SqlState: "23505", ConstraintName: "IX_UserPreferences_UserId" })
            {
                // Another first-load request created this same user's defaults.
                _dbContext.Entry(preferences).State = EntityState.Detached;
                preferences = await _dbContext.Set<DomainUserPreferences>().AsNoTracking()
                    .SingleOrDefaultAsync(x => x.UserId == currentUserId && !x.IsDeleted, cancellationToken)
                    ?? throw new UserManagementException("Preferences are unavailable. Please try again.");
            }
        }

        return Map(preferences);
    }

    public async Task<UserPreferencesResponse> UpdateAsync(
        Guid currentUserId,
        UpdateUserPreferencesRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureActiveUserAsync(
            currentUserId,
            cancellationToken);

        Validate(request);

        var preferences = await _dbContext.Set<DomainUserPreferences>()
            .SingleOrDefaultAsync(
                x => x.UserId == currentUserId && !x.IsDeleted,
                cancellationToken);

        if (preferences is null)
        {
            preferences = new DomainUserPreferences
            {
                UserId = currentUserId,
                CreatedBy = currentUserId
            };

            _dbContext.Set<DomainUserPreferences>().Add(preferences);
        }

        preferences.Theme = request.Theme.Trim();
        preferences.Language = request.Language.Trim();
        preferences.TimeZone = request.TimeZone.Trim();
        preferences.DateFormat = request.DateFormat.Trim();
        preferences.TimeFormat = request.TimeFormat.Trim();
        preferences.NumberFormat = request.NumberFormat.Trim();

        preferences.CompactMode = request.CompactMode;
        preferences.ConfirmActions = request.ConfirmActions;
        preferences.RememberNavigation = request.RememberNavigation;

        preferences.EmailNotificationsEnabled = request.EmailNotificationsEnabled;
        preferences.InAppNotificationsEnabled = request.InAppNotificationsEnabled;
        preferences.PushNotificationsEnabled = request.PushNotificationsEnabled;
        preferences.SecurityAlertsEnabled = request.SecurityAlertsEnabled;

        preferences.ProjectNotificationsEnabled = request.ProjectNotificationsEnabled;
        preferences.ApprovalNotificationsEnabled = request.ApprovalNotificationsEnabled;
        preferences.InventoryNotificationsEnabled = request.InventoryNotificationsEnabled;
        preferences.HRNotificationsEnabled = request.HRNotificationsEnabled;
        preferences.FinanceNotificationsEnabled = request.FinanceNotificationsEnabled;

        preferences.AnalyticsConsent = request.AnalyticsConsent;
        preferences.ProductImprovementConsent = request.ProductImprovementConsent;

        preferences.UpdatedBy = currentUserId;
        preferences.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Map(preferences);
    }

    private async Task EnsureActiveUserAsync(
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var isActive = await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(
                x => x.Id == currentUserId &&
                     x.IsActive &&
                     !x.IsDeleted,
                cancellationToken);

        if (!isActive)
        {
            throw new UserManagementException(
                "The authenticated user account is unavailable.");
        }
    }

    private static void Validate(
        UpdateUserPreferencesRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        RequireValue(request.Theme, nameof(request.Theme), 20);
        RequireValue(request.Language, nameof(request.Language), 10);
        RequireValue(request.TimeZone, nameof(request.TimeZone), 100);
        RequireValue(request.DateFormat, nameof(request.DateFormat), 30);
        RequireValue(request.TimeFormat, nameof(request.TimeFormat), 10);
        RequireValue(request.NumberFormat, nameof(request.NumberFormat), 20);

        if (!new[] { "System", "Light", "Dark" }.Contains(
                request.Theme.Trim(),
                StringComparer.OrdinalIgnoreCase))
        {
            throw new UserManagementException(
                "Theme must be System, Light, or Dark.");
        }

        if (!new[] { "en", "am", "om" }.Contains(request.Language.Trim(), StringComparer.Ordinal))
            throw new UserManagementException("Language must be English, Amharic, or Afaan Oromo.");
        if (!new[] { "dd/MM/yyyy", "MM/dd/yyyy", "yyyy-MM-dd" }.Contains(request.DateFormat.Trim(), StringComparer.Ordinal))
            throw new UserManagementException("Choose a supported date format.");
        try { TimeZoneInfo.FindSystemTimeZoneById(request.TimeZone.Trim()); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        { throw new UserManagementException("Choose a valid time zone."); }
        if (!System.Globalization.CultureInfo.GetCultures(System.Globalization.CultureTypes.AllCultures)
            .Any(x => x.Name.Length > 0 && string.Equals(x.Name, request.NumberFormat.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new UserManagementException("Choose a valid number format culture.");

        if (!new[] { "12h", "24h" }.Contains(
                request.TimeFormat.Trim(),
                StringComparer.OrdinalIgnoreCase))
        {
            throw new UserManagementException(
                "Time format must be 12h or 24h.");
        }
    }

    private static void RequireValue(
        string? value,
        string fieldName,
        int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new UserManagementException(
                $"{fieldName} is required.");
        }

        if (value.Trim().Length > maxLength)
        {
            throw new UserManagementException(
                $"{fieldName} cannot exceed {maxLength} characters.");
        }
    }

    private static UserPreferencesResponse Map(
        DomainUserPreferences preferences)
    {
        return new UserPreferencesResponse(
            preferences.UserId,
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
            preferences.ProductImprovementConsent);
    }
}