using System.Security.Claims;
using CSM.Application.Common.Exceptions;
using CSM.Application.Common.Security;
using CSM.Application.HRM.Payroll;
using CSM.Application.HRM.Payroll.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CSM.Application.Common.Audit;


namespace CSM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class PayrollController : ControllerBase
{
    private readonly IPayrollService _payrollService;
    private readonly IAuditService _auditService;

    public PayrollController(
        IPayrollService payrollService,
        IAuditService auditService)
    {
        _payrollService = payrollService;
        _auditService = auditService;
    }

    [HttpGet]
    [Authorize(Policy = "HRM.Payroll.View")]
    public async Task<
        ActionResult<IReadOnlyCollection<PayrollResponse>>> GetAll(
        [FromQuery] Guid? employeeId = null,
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var records =
                await _payrollService.GetAllAsync(
                    GetCurrentUserId(),
                    employeeId,
                    fromDate,
                    toDate,
                    cancellationToken);

            return Ok(records);
        }
        catch (PayrollManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{payrollRecordId:guid}")]
    [Authorize(Policy = "HRM.Payroll.View")]
    public async Task<ActionResult<PayrollResponse>> GetById(
        Guid payrollRecordId,
        CancellationToken cancellationToken)
    {
        try
        {
            var record =
                await _payrollService.GetByIdAsync(
                    GetCurrentUserId(),
                    payrollRecordId,
                    cancellationToken);

            return Ok(record);
        }
        catch (PayrollManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult<PayrollResponse>> Create(
        CreatePayrollRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var record =
                await _payrollService.CreateAsync(
                    GetCurrentUserId(),
                    request,
                    cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { payrollRecordId = record.Id },
                record);
        }
        catch (PayrollManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{payrollRecordId:guid}/calculate")]
    public async Task<ActionResult<PayrollResponse>> Calculate(
        Guid payrollRecordId,
        CalculatePayrollRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var record =
                await _payrollService.CalculateAsync(
                    GetCurrentUserId(),
                    payrollRecordId,
                    request,
                    cancellationToken);

            return Ok(record);
        }
        catch (PayrollManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{payrollRecordId:guid}/approve")]
    [Authorize(Policy = "HRM.Payroll.Approve")]
    public async Task<ActionResult<PayrollResponse>> Approve(
        Guid payrollRecordId,
        ApprovePayrollRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var record =
                await _payrollService.ApproveAsync(
                    GetCurrentUserId(),
                    payrollRecordId,
                    request,
                    cancellationToken);

            await _auditService.LogAsync(
                GetCurrentUserId(),
                User.Identity?.Name ?? "System",
                "Payroll",
                "Approve",
                "Payroll",
                record.Id.ToString(),
                $"Approved payroll for {record.EmployeeName}",
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                cancellationToken);

            return Ok(record);
        }
        catch (PayrollManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{payrollRecordId:guid}/paid")]
    [Authorize(Policy = "HRM.Payroll.Approve")]
    public async Task<ActionResult<PayrollResponse>> MarkPaid(
        Guid payrollRecordId,
        MarkPayrollPaidRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var record =
                await _payrollService.MarkPaidAsync(
                    GetCurrentUserId(),
                    payrollRecordId,
                    request,
                    cancellationToken);

            await _auditService.LogAsync(
                GetCurrentUserId(),
                User.Identity?.Name ?? "System",
                "Payroll",
                "Paid",
                "Payroll",
                record.Id.ToString(),
                $"Marked payroll for {record.EmployeeName} as paid",
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                cancellationToken);

            return Ok(record);
        }
        catch (PayrollManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{payrollRecordId:guid}/cancel")]
    public async Task<ActionResult<PayrollResponse>> Cancel(
        Guid payrollRecordId,
        CancelPayrollRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var record =
                await _payrollService.CancelAsync(
                    GetCurrentUserId(),
                    payrollRecordId,
                    request,
                    cancellationToken);

            await _auditService.LogAsync(
                GetCurrentUserId(),
                User.Identity?.Name ?? "System",
                "Payroll",
                "Cancel",
                "Payroll",
                record.Id.ToString(),
                $"Cancelled payroll for {record.EmployeeName}",
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                cancellationToken);

            return Ok(record);
        }
        catch (PayrollManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{payrollRecordId:guid}/adjustments")]
    public async Task<ActionResult<PayrollResponse>> AddAdjustment(
        Guid payrollRecordId,
        CreatePayrollAdjustmentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var record =
                await _payrollService.AddAdjustmentAsync(
                    GetCurrentUserId(),
                    payrollRecordId,
                    request,
                    cancellationToken);

            return Ok(record);
        }
        catch (PayrollManagementException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
    [HttpGet("{payrollRecordId:guid}/payslip")]
    [Authorize(Policy = "HRM.Payroll.View")]
    public async Task<IActionResult> GetPayslip(
        Guid payrollRecordId,
        CancellationToken cancellationToken)
    {
        var pdf = await _payrollService.GeneratePayslipAsync(
            GetCurrentUserId(),
            payrollRecordId,
            cancellationToken);

        return File(
            pdf,
            "application/pdf",
            $"Payslip-{payrollRecordId}.pdf");
    }




    private Guid GetCurrentUserId()
    {
        var value =
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(value, out var userId))
        {
            throw new PayrollManagementException(
                "Authenticated user identifier is missing.");
        }

        return userId;
    }
}
