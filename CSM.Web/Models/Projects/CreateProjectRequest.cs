namespace CSM.Web.Models.Projects;

public class CreateProjectRequest
{
    public Guid ConstructionSiteId { get; set; }

    public string ProjectCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateOnly? PlannedStartDate { get; set; }

    public DateOnly? PlannedEndDate { get; set; }

    public decimal ContractValue { get; set; }
}