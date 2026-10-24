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

        var companyIdValue = User.FindFirst("company_id")?.Value;

        if (!Guid.TryParse(companyIdValue, out var companyId) ||
            companyId == Guid.Empty)
        {
            return Forbid();
        }

        var hasPermission =
            await _permissionEvaluator.HasPermissionAsync(
                userId,
                "Data_Export",
                cancellationToken);

        if (!hasPermission)
        {
            return Forbid();
        }

        var account = await _dbContext.Users
            .AsNoTracking()
            .Where(user =>
                user.Id == userId &&
                user.CompanyId == companyId &&
                !user.IsDeleted)
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
            Account = account
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
