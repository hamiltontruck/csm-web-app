using CSM.Web.Models.HRM;

namespace CSM.Web.Services;

public interface IEmployeeService
{
    Task<IReadOnlyList<EmployeeResponse>> GetEmployeesAsync(
        string? status = null,
        string? employeeScope = null);

    Task<EmployeeResponse> GetEmployeeAsync(
        Guid employeeId);

    Task<EmployeeResponse> CreateEmployeeAsync(
        CreateEmployeeRequest request);

    Task<EmployeeResponse> UpdateEmployeeAsync(
        Guid employeeId,
        UpdateEmployeeRequest request);

    Task<EmployeeResponse> ChangeEmployeeStatusAsync(
        Guid employeeId,
        ChangeEmployeeStatusRequest request);
}