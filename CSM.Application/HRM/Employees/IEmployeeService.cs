using CSM.Application.HRM.Employees.Dtos;
using CSM.Domain.Enums;

namespace CSM.Application.HRM.Employees;

public interface IEmployeeService
{
    Task<IReadOnlyCollection<EmployeeResponse>> GetAllAsync(
        Guid currentUserId,
        EmployeeStatus? status = null,
        EmployeeScope? employeeScope = null,
        CancellationToken cancellationToken = default);

    Task<EmployeeResponse> GetByIdAsync(
        Guid currentUserId,
        Guid employeeId,
        CancellationToken cancellationToken = default);

    Task<EmployeeResponse> CreateAsync(
        Guid currentUserId,
        CreateEmployeeRequest request,
        CancellationToken cancellationToken = default);

    Task<EmployeeResponse> UpdateAsync(
        Guid currentUserId,
        Guid employeeId,
        UpdateEmployeeRequest request,
        CancellationToken cancellationToken = default);

    Task<EmployeeResponse> ChangeStatusAsync(
        Guid currentUserId,
        Guid employeeId,
        ChangeEmployeeStatusRequest request,
        CancellationToken cancellationToken = default);
}