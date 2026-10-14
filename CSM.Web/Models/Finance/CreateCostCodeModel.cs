namespace CSM.Web.Models.Finance;

public sealed class CreateCostCodeResponse
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public Guid? ParentCostCodeId { get; set; }

    public Guid? ConstructionSiteId { get; set; }
}