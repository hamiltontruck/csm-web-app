using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Application.Common.Security;
using CSM.Application.Procurement.EquipmentUtilization;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = AppPolicies.ProcurementManagement)]
public sealed class EquipmentUtilizationController : ControllerBase
{
    private readonly EquipmentUtilizationService _service;
    private readonly ApplicationDbContext _dbContext;

    public EquipmentUtilizationController(
        EquipmentUtilizationService service,
        ApplicationDbContext dbContext)
    {
        _service = service;
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EquipmentUtilizationResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetCurrentUserId();

            var user = await _dbContext.Users
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.Id == userId,
                    cancellationToken);

            if (user is null)
            {
                throw new EquipmentManagementException(
                    "Authenticated user was not found.");
            }

            if (!user.CompanyId.HasValue)
            {
                throw new EquipmentManagementException(
                    "Current user is not assigned to a company.");
            }

            var result = await _service.GetAllAsync(
                user.CompanyId.Value,
                cancellationToken);

            return Ok(result);
        }
        catch (EquipmentManagementException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    private Guid GetCurrentUserId()
    {
        var value =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            User.FindFirstValue("sub");

        if (!Guid.TryParse(
                value,
                out var userId))
        {
            throw new EquipmentManagementException(
                "Current user identity is invalid.");
        }

        return userId;
    }
}