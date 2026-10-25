
using System.Security.Claims;
using CSM.Domain.Entities.Identity;
using CSM.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/privacy-data-requests")]
[Authorize]
public sealed class PrivacyDataRequestsController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;

    public PrivacyDataRequestsController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("mine")]
    public async Task<IActionResult> GetMine(
        CancellationToken cancellationToken)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var companyIdValue = User.FindFirst("company_id")?.Value;

        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return Unauthorized();
        }

        if (!Guid.TryParse(companyIdValue, out var companyId) ||
            companyId == Guid.Empty)
        {
            return Forbid();
        }

        var requests = await _dbContext.Set<PrivacyDataRequest>()
            .AsNoTracking()
            .Where(request =>
                request.UserId == userId &&
                request.CompanyId == companyId)
            .OrderByDescending(request => request.RequestedAtUtc)
            .Select(request => new
            {
                request.Id,
                request.RequestType,
                request.Status,
                request.Description,
                request.RequestedAtUtc,
                request.CompletedAtUtc,
                request.ResolutionNotes
            })
            .ToListAsync(cancellationToken);

        return Ok(requests);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreatePrivacyDataRequest input,
        CancellationToken cancellationToken)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var companyIdValue = User.FindFirst("company_id")?.Value;

        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return Unauthorized();
        }

        if (!Guid.TryParse(companyIdValue, out var companyId) ||
            companyId == Guid.Empty)
        {
            return Forbid();
        }

        var allowedRequestTypes = new[]
        {
            "DataAccess",
            "DataCorrection",
            "DataDeletion",
            "PrivacyInquiry"
        };

        if (string.IsNullOrWhiteSpace(input.RequestType) ||
            !allowedRequestTypes.Contains(
                input.RequestType,
                StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message = "The requested privacy request type is invalid."
            });
        }

        var description = input.Description?.Trim();

        if (description?.Length > 2000)
        {
            return BadRequest(new
            {
                message = "Description cannot exceed 2000 characters."
            });
        }

        var accountExists = await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(user =>
                user.Id == userId &&
                user.CompanyId == companyId &&
                !user.IsDeleted,
                cancellationToken);

        if (!accountExists)
        {
            return Unauthorized();
        }

        var request = new PrivacyDataRequest
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CompanyId = companyId,
            RequestType = allowedRequestTypes.First(type =>
                string.Equals(
                    type,
                    input.RequestType,
                    StringComparison.OrdinalIgnoreCase)),
            Status = "Submitted",
            Description = description,
            RequestedAtUtc = DateTime.UtcNow
        };

        _dbContext.Set<PrivacyDataRequest>().Add(request);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(
            nameof(GetMine),
            new { },
            new
            {
                request.Id,
                request.RequestType,
                request.Status,
                request.RequestedAtUtc
            });
    }

    public sealed class CreatePrivacyDataRequest
    {
        public string RequestType { get; set; } = string.Empty;

        public string? Description { get; set; }
    }
}
