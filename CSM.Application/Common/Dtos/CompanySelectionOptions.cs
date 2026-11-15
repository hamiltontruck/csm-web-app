namespace CSM.Application.Common.Dtos;

public sealed class CompanySelectionOptions
{
    public bool CanSelectCompany { get; set; }
    public Guid? CompanyId { get; set; }
    public List<CompanySelectionOption> Companies { get; set; } = [];
}

public sealed class CompanySelectionOption
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
}
