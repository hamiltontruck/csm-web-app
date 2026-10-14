using CSM.Application.Payroll;
using CSM.Application.Payroll.Dtos;
using CSM.Domain.Entities.HRM;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public sealed class SalaryStructureService : ISalaryStructureService
{
    private readonly ApplicationDbContext _context;

    public SalaryStructureService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<SalaryStructureDto?> GetByEmployeeAsync(
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        var entity = await _context.SalaryStructures
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.EmployeeId == employeeId,
                cancellationToken);

        return entity is null ? null : Map(entity);
    }

    public async Task<SalaryStructureDto> CreateOrUpdateAsync(
        UpdateSalaryStructureRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await _context.SalaryStructures
            .FirstOrDefaultAsync(
                x => x.EmployeeId == request.EmployeeId,
                cancellationToken);

        if (entity is null)
        {
            entity = new SalaryStructure
            {
                EmployeeId = request.EmployeeId
            };

            _context.SalaryStructures.Add(entity);
        }

        entity.BasicSalary = request.BasicSalary;
        entity.HousingAllowance = request.HousingAllowance;
        entity.TransportAllowance = request.TransportAllowance;
        entity.PositionAllowance = request.PositionAllowance;
        entity.OvertimeMultiplier = request.OvertimeMultiplier;
        entity.PensionPercentage = request.PensionPercentage;
        entity.BankName = request.BankName;
        entity.BankAccountNumber = request.BankAccountNumber;
        entity.IsActive = request.IsActive;

        await _context.SaveChangesAsync(cancellationToken);

        return Map(entity);
    }

    private static SalaryStructureDto Map(SalaryStructure entity)
    {
        return new SalaryStructureDto
        {
            Id = entity.Id,
            EmployeeId = entity.EmployeeId,
            BasicSalary = entity.BasicSalary,
            HousingAllowance = entity.HousingAllowance,
            TransportAllowance = entity.TransportAllowance,
            PositionAllowance = entity.PositionAllowance,
            OvertimeMultiplier = entity.OvertimeMultiplier,
            PensionPercentage = entity.PensionPercentage,
            BankName = entity.BankName,
            BankAccountNumber = entity.BankAccountNumber,
            IsActive = entity.IsActive
        };
    }
}