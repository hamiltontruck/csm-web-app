using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Domain.Constants;
using CSM.Application.HRM.Employees;
using CSM.Application.HRM.Employees.Dtos;
using CSM.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CSM.Application.Common.Audit;



namespace CSM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = AppRoles.SuperAdmin + "," + AppRoles.CompanyAdmin + "," +
    AppRoles.HrOfficer + "," + AppRoles.HROfficer)]
public sealed class EmployeesController : ControllerBase
{
    private readonly IEmployeeService _employeeService;
    private readonly IAuditService _auditService;

    public EmployeesController(
        IEmployeeService employeeService,
        IAuditService auditService)
    {
        _employeeService = employeeService;
        _auditService = auditService;
    }

    [HttpGet]
    public async Task<
        ActionResult<IReadOnlyCollection<EmployeeResponse>>> GetAll(
        [FromQuery] EmployeeStatus? status,
        [FromQuery] EmployeeScope? employeeScope,
        CancellationToken cancellationToken)
    {
        try
        {
            var employees =
                await _employeeService.GetAllAsync(
                    GetCurrentUserId(),
                    status,
                    employeeScope,
                    cancellationToken);

            return Ok(employees);
        }
        catch (EmployeeManagementException ex)
        {
            return BadRequest(
                new { message = ex.Message });
        }
    }

    [HttpGet("{employeeId:guid}")]
    public async Task<ActionResult<EmployeeResponse>> GetById(
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        try
        {
            var employee =
                await _employeeService.GetByIdAsync(
                    GetCurrentUserId(),
                    employeeId,
                    cancellationToken);

            return Ok(employee);
        }
        catch (EmployeeManagementException ex)
        {
            return BadRequest(
                new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult<EmployeeResponse>> Create(
        CreateEmployeeRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var employee =
                await _employeeService.CreateAsync(
                    GetCurrentUserId(),
                    request,
                    cancellationToken);

                await _auditService.LogAsync(
                    GetCurrentUserId(),
                    User.Identity?.Name ?? "System",
                    "HRM",
                    "Create",
                    "Employee",
                    employee.Id.ToString(),
                    $"Created employee {employee.EmployeeNumber}",
                    HttpContext.Connection.RemoteIpAddress?.ToString(),
                    cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { employeeId = employee.Id },
                employee);
        }
        catch (EmployeeManagementException ex)
        {
            return BadRequest(
                new { message = ex.Message });
        }
    }

    [HttpPut("{employeeId:guid}")]
    public async Task<ActionResult<EmployeeResponse>> Update(
        Guid employeeId,
        UpdateEmployeeRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var employee =
                await _employeeService.UpdateAsync(
                    GetCurrentUserId(),
                    employeeId,
                    request,
                    cancellationToken);

                await _auditService.LogAsync(
                    GetCurrentUserId(),
                    User.Identity?.Name ?? "System",
                    "HRM",
                    "Update",
                    "Employee",
                    employee.Id.ToString(),
                    $"Updated employee {employee.EmployeeNumber}",
                    HttpContext.Connection.RemoteIpAddress?.ToString(),
                    cancellationToken);

            return Ok(employee);
        }
        catch (EmployeeManagementException ex)
        {
            return BadRequest(
                new { message = ex.Message });
        }
    }

    [HttpPut("{employeeId:guid}/status")]
    public async Task<ActionResult<EmployeeResponse>> ChangeStatus(
        Guid employeeId,
        ChangeEmployeeStatusRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var employee =
                await _employeeService.ChangeStatusAsync(
                    GetCurrentUserId(),
                    employeeId,
                    request,
                    cancellationToken);

                await _auditService.LogAsync(
                    GetCurrentUserId(),
                    User.Identity?.Name ?? "System",
                    "HRM",
                    "Status Change",
                    "Employee",
                    employee.Id.ToString(),
                    $"Changed status to {request.Status}",
                    HttpContext.Connection.RemoteIpAddress?.ToString(),
                    cancellationToken);

            return Ok(employee);
        }
        catch (EmployeeManagementException ex)
        {
            return BadRequest(
                new { message = ex.Message });
        }
    }

    private Guid GetCurrentUserId()
    {
        var value = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(value, out var userId))
        {
            throw new EmployeeManagementException(
                "Authenticated user identifier is invalid.");
        }

        return userId;
    }
}