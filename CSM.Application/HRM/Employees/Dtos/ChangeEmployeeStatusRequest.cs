using CSM.Domain.Enums;

namespace CSM.Application.HRM.Employees.Dtos;

public sealed record ChangeEmployeeStatusRequest(
    EmployeeStatus Status,
    DateOnly? TerminationDate);