using CSM.Application.Payroll.Calculations;
using CSM.Application.Payroll.Calculations.Dtos;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public sealed class PayrollCalculationService : IPayrollCalculationService
{
    private readonly ApplicationDbContext _context;

    public PayrollCalculationService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PayrollCalculationResponse> CalculateAsync(
        Guid currentUserId,
        CalculatePayrollRequest request,
        CancellationToken cancellationToken)
    {
        var employee = await PayrollAuxiliaryAccess.GetEmployeeAsync(
            _context, currentUserId, request.EmployeeId, manage: true, cancellationToken);

        var salary = await _context.SalaryStructures
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.EmployeeId == employee.Id && x.IsActive,
                cancellationToken);

        if (salary is null)
            throw new KeyNotFoundException("Salary structure was not found.");

        // Monthly salary → hourly rate
        var hourlyRate = salary.BasicSalary / (30m * 8m);

        // Overtime pay
        var overtimeAmount = request.OvertimeHours <= 0
            ? 0m
            : hourlyRate * request.OvertimeHours * salary.OvertimeMultiplier;

        // Gross salary
        var grossSalary =
            salary.BasicSalary +
            salary.HousingAllowance +
            salary.TransportAllowance +
            salary.PositionAllowance +
            overtimeAmount;

        var calculatedPension =
            grossSalary * salary.PensionPercentage / 100m;

        var pension =
            request.PensionDeduction ?? calculatedPension;

        var tax =
            request.TaxDeduction ?? 0m;

        var other =
            request.OtherDeduction ?? 0m;

        var totalDeductions =
            pension + tax + other;

        var netSalary =
            grossSalary - totalDeductions;

        return new PayrollCalculationResponse
        {
            EmployeeId = employee.Id,
            EmployeeName = $"{employee.FirstName} {employee.LastName}",

            BasicSalary = salary.BasicSalary,
            HousingAllowance = salary.HousingAllowance,
            TransportAllowance = salary.TransportAllowance,
            PositionAllowance = salary.PositionAllowance,

            OvertimeHours = request.OvertimeHours,
            OvertimeAmount = overtimeAmount,

            GrossSalary = grossSalary,

            PensionDeduction = pension,
            TaxDeduction = tax,

            OtherDeduction = other,
            TotalDeductions = totalDeductions,
            NetSalary = netSalary
        };
    }
}