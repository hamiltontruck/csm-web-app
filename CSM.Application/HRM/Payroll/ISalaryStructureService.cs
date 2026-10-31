using CSM.Application.Payroll.Dtos;

namespace CSM.Application.Payroll;

public interface ISalaryStructureService
{
    Task<SalaryStructureDto?> GetByEmployeeAsync(
        Guid currentUserId,
        Guid employeeId,
        CancellationToken cancellationToken);

    Task<SalaryStructureDto> CreateOrUpdateAsync(
        Guid currentUserId,
        UpdateSalaryStructureRequest request,
        CancellationToken cancellationToken);
}