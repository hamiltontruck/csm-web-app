namespace CSM.Web.Models.Finance;

public sealed class UpdateCostCodeResponse
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public Guid? ParentCostCodeId { get; set; }
}