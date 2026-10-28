using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Domain.Constants;
using CSM.Application.HRM.SiteAssignments;
using CSM.Application.HRM.SiteAssignments.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class SiteAssignmentsController : ControllerBase
{
    private const string ReadRoles = AppRoles.SuperAdmin + "," + AppRoles.CompanyAdmin + "," +
        AppRoles.SiteManager + "," + AppRoles.Supervisor + "," + AppRoles.HrOfficer + "," + AppRoles.HROfficer;
    private const string ManageRoles = AppRoles.SuperAdmin + "," + AppRoles.CompanyAdmin + "," +
        AppRoles.HrOfficer + "," + AppRoles.HROfficer;

    private readonly ISiteAssignmentService _siteAssignmentService;

    public SiteAssignmentsController(
        ISiteAssignmentService siteAssignmentService)
    {
        _siteAssignmentService = siteAssignmentService;
    }

    [HttpGet]
    [Authorize(Policy = "HRM.SiteAssignment.View", Roles = ReadRoles)]
    public async Task<
        ActionResult<IReadOnlyCollection<SiteAssignmentResponse>>> GetAll(
        [FromQuery] Guid? employeeId,
        [FromQuery] Guid? constructionSiteId,
        CancellationToken cancellationToken)
    {
        try
        {
            var assignments =
                await _siteAssignmentService.GetAllAsync(
                    GetCurrentUserId(),
                    employeeId,
                    constructionSiteId,
                    cancellationToken);

            return Ok(assignments);
        }
        catch (SiteAssignmentManagementException ex)
        {
            return ErrorResult(ex);
        }
    }

    [HttpGet("{assignmentId:guid}")]
    [Authorize(Policy = "HRM.SiteAssignment.View", Roles = ReadRoles)]
    public async Task<ActionResult<SiteAssignmentResponse>> GetById(
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        try
        {
            var assignment =
                await _siteAssignmentService.GetByIdAsync(
                    GetCurrentUserId(),
                    assignmentId,
                    cancellationToken);

            return Ok(assignment);
        }
        catch (SiteAssignmentManagementException ex)
        {
            return ErrorResult(ex);
        }
    }

    [HttpPost]
    [Authorize(Policy = "HRM.SiteAssignment.Manage", Roles = ManageRoles)]
    public async Task<ActionResult<SiteAssignmentResponse>> Create(
        CreateSiteAssignmentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var assignment =
                await _siteAssignmentService.CreateAsync(
                    GetCurrentUserId(),
                    request,
                    cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new
                {
                    assignmentId = assignment.Id
                },
                assignment);
        }
        catch (SiteAssignmentManagementException ex)
        {
            return ErrorResult(ex);
        }
    }

    [HttpPut("{assignmentId:guid}")]
    [Authorize(Policy = "HRM.SiteAssignment.Manage", Roles = ManageRoles)]
    public async Task<ActionResult<SiteAssignmentResponse>> Update(
        Guid assignmentId,
        UpdateSiteAssignmentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var assignment =
                await _siteAssignmentService.UpdateAsync(
                    GetCurrentUserId(),
                    assignmentId,
                    request,
                    cancellationToken);

            return Ok(assignment);
        }
        catch (SiteAssignmentManagementException ex)
        {
            return ErrorResult(ex);
        }
    }

    private ActionResult ErrorResult(SiteAssignmentManagementException ex)
    {
        var body = new { message = ex.Message };
        return ex.Message switch
        {
            "You are not authorized to access site assignments." or
            "You are not authorized to create or modify site assignments." or
            "Current user is not assigned to a company." or
            "Current user was not found or is inactive." or
            "Authenticated user identifier is invalid." or
            "You cannot access site assignments outside your company." or
            "Employee does not belong to your company." or
            "Construction site does not belong to your company." => StatusCode(403, body),
            "Site assignment was not found." or
            "Employee was not found." or
            "Construction site was not found." => NotFound(body),
            _ => BadRequest(body)
        };
    }

    private Guid GetCurrentUserId()
    {
        var userId =
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userId, out var parsedUserId))
        {
            throw new SiteAssignmentManagementException(
                "Authenticated user identifier is invalid.");
        }

        return parsedUserId;
    }
}