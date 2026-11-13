namespace CSM.Application.HRM.Employees.Dtos;

public sealed class EmployeeCreateOptions
{
    public bool CanSelectCompany { get; set; }
    public Guid? CompanyId { get; set; }
    public List<EmployeeCompanyOption> Companies { get; set; } = [];
}

public sealed class EmployeeCompanyOption
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
}
