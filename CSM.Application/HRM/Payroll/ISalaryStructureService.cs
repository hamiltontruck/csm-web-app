using CSM.Application.Payroll.Dtos;

namespace CSM.Application.Payroll;

public interface ISalaryStructureService
{
    Task<SalaryStructureDto?> GetByEmployeeAsync(
        Guid employeeId,
        CancellationToken cancellationToken);

    Task<SalaryStructureDto> CreateOrUpdateAsync(
        UpdateSalaryStructureRequest request,
        CancellationToken cancellationToken);
}