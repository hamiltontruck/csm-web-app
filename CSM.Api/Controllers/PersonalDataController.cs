using System.Security.Claims;
using System.Text.Json;
using CSM.Domain.Entities.Identity;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/personal-data")]
[Authorize]
public sealed class PersonalDataController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IPermissionEvaluator _permissionEvaluator;

    public PersonalDataController(
        ApplicationDbContext dbContext,
        IPermissionEvaluator permissionEvaluator)
    {
        _dbContext = dbContext;
        _permissionEvaluator = permissionEvaluator;
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(
        CancellationToken cancellationToken)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return Unauthorized();
        }

        var actor = await _dbContext.Users.AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == userId && x.IsActive && !x.IsDeleted, cancellationToken);
        if (actor is null) return Unauthorized();
        var companyClaim = User.FindFirst("company_id")?.Value;
        if (!string.IsNullOrWhiteSpace(companyClaim) &&
            (!Guid.TryParse(companyClaim, out var companyId) || actor.CompanyId != companyId)) return Forbid();

        var account = await _dbContext.Users
            .AsNoTracking()
            .Where(user =>
                user.Id == userId &&
                user.IsActive && !user.IsDeleted)
            .Select(user => new
            {
                user.Id,
                user.CompanyId,
                user.EmployeeId,
                user.Email,
                user.FirstName,
                user.LastName,
                user.PhoneNumber,
                user.JobTitle,
                user.IsActive,
                user.MustChangePassword,
                user.LastLoginAtUtc,
                user.DefaultSiteId,
                user.CreatedAtUtc,
                user.UpdatedAtUtc
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (account is null)
        {
            return NotFound();
        }

        var export = new
        {
            ExportedAtUtc = DateTime.UtcNow,
            DataScope = "Personal account information",
            Account = account,
            Preferences = await _dbContext.Set<UserPreferences>().AsNoTracking()
                .Where(x => x.UserId == userId && !x.IsDeleted)
                .Select(x => new { x.Theme, x.Language, x.TimeZone, x.DateFormat, x.TimeFormat, x.NumberFormat,
                    x.CompactMode, x.ConfirmActions, x.RememberNavigation, x.EmailNotificationsEnabled,
                    x.InAppNotificationsEnabled, x.PushNotificationsEnabled, x.SecurityAlertsEnabled,
                    x.ProjectNotificationsEnabled, x.ApprovalNotificationsEnabled, x.InventoryNotificationsEnabled,
                    x.HRNotificationsEnabled, x.FinanceNotificationsEnabled, x.AnalyticsConsent, x.ProductImprovementConsent })
                .SingleOrDefaultAsync(cancellationToken)
        };

        var json = JsonSerializer.SerializeToUtf8Bytes(
            export,
            new JsonSerializerOptions
            {
                WriteIndented = true
            });

        return File(
            json,
            "application/json; charset=utf-8",
            $"personal-data-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json");
    }
}
