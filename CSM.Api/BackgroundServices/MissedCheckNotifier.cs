using CSM.Application.Notifications;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Api.BackgroundServices;

/// <summary>
/// Every 15 minutes, once the working day is under way, finds sites where
/// assigned employees have not checked in and tells the people who approve
/// attendance for that site. One alert per site per day.
/// </summary>
public sealed class MissedCheckInNotifier : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    private readonly IServiceScopeFactory _scopes;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MissedCheckInNotifier> _logger;

    public MissedCheckInNotifier(
        IServiceScopeFactory scopes,
        IConfiguration configuration,
        ILogger<MissedCheckInNotifier> logger)
    {
        _scopes = scopes;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_configuration.GetValue("Attendance:MissedCheckIn:Enabled", true))
        {
            return;
        }

        using var timer = new PeriodicTimer(Interval);

        try
        {
            do
            {
                try
                {
                    await RunOnceAsync(stoppingToken);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    // Never let one failed run stop the job.
                    _logger.LogError(ex, "Missed check-in check failed.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // Application is shutting down.
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        const string section = "Attendance:MissedCheckIn:";

        // Local time at the sites. Default is East Africa Time, UTC+3.
        var offset = TimeSpan.FromHours(
            _configuration.GetValue<double>(section + "LocalUtcOffsetHours", 3));

        var cutoff = TimeOnly.Parse(
            _configuration[section + "CutoffLocalTime"] ?? "09:30");

        var endOfDay = TimeOnly.Parse(
            _configuration[section + "EndLocalTime"] ?? "17:00");

        var workingDays = (_configuration[section + "WorkingDays"]
                ?? "Monday,Tuesday,Wednesday,Thursday,Friday,Saturday")
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(day => Enum.Parse<DayOfWeek>(day))
            .ToHashSet();

        var localNow = DateTime.UtcNow + offset;
        var today = DateOnly.FromDateTime(localNow);
        var localTime = TimeOnly.FromDateTime(localNow);

        if (!workingDays.Contains(localNow.DayOfWeek) ||
            localTime < cutoff ||
            localTime > endOfDay)
        {
            return;
        }

        var startOfTodayUtc = DateTime.SpecifyKind(
            today.ToDateTime(TimeOnly.MinValue) - offset,
            DateTimeKind.Utc);

        using var scope = _scopes.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<INotificationPublisher>();

        // Assigned today, active employee, active site, no attendance row
        // for today, and not on approved leave.
        var missing = await (
                from a in db.SiteAssignments
                join e in db.Employees on a.EmployeeId equals e.Id
                join s in db.ConstructionSites on a.ConstructionSiteId equals s.Id
                where a.StartDate <= today
                      && (!a.EndDate.HasValue || a.EndDate.Value >= today)
                      && e.Status == EmployeeStatus.Active
                      && s.IsActive
                      && !db.Attendances.Any(t =>
                          t.EmployeeId == a.EmployeeId
                          && t.ConstructionSiteId == a.ConstructionSiteId
                          && t.AttendanceDate == today)
                      && !db.LeaveRequests.Any(l =>
                          l.EmployeeId == a.EmployeeId
                          && l.Status == LeaveStatus.Approved
                          && l.StartDate <= today
                          && l.EndDate >= today)
                group a by new { a.CompanyId, a.ConstructionSiteId, s.Name } into g
                select new
                {
                    g.Key.CompanyId,
                    SiteId = g.Key.ConstructionSiteId,
                    SiteName = g.Key.Name,
                    Count = g.Select(x => x.EmployeeId).Distinct().Count()
                })
            .AsNoTracking()
            .ToListAsync(ct);

        foreach (var site in missing)
        {
            // One alert per site per day.
            var alreadySent = await db.Notifications.AnyAsync(
                n => n.CompanyId == site.CompanyId
                     && n.SourceEntityType == "MissedCheckIn"
                     && n.SourceEntityId == site.SiteId
                     && n.CreatedAtUtc >= startOfTodayUtc,
                ct);

            if (alreadySent)
            {
                continue;
            }

            await publisher.StageForPermissionHoldersAsync(
                site.CompanyId,
                site.SiteId,
                GranularPermission.HRM_Attendance_Approve,
                excludeUserId: null,
                title: "Employees have not checked in",
                message: $"{site.Count} assigned employee(s) have not " +
                         $"checked in at {site.SiteName} today.",
                severity: NotificationSeverity.Warning,
                sourceEntityType: "MissedCheckIn",
                sourceEntityId: site.SiteId,
                cancellationToken: ct);
        }

        await db.SaveChangesAsync(ct);
    }
}
