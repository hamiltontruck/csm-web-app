namespace CSM.Application.Procurement.Equipment.Dtos;

public sealed class EquipmentCreateOptions
{
    public bool CanSelectCompany { get; set; }
    public Guid? CompanyId { get; set; }
    public List<EquipmentCompanyOption> Companies { get; set; } = [];
}

public sealed class EquipmentCompanyOption
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
}
