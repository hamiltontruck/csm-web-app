namespace CSM.Web.Models.Projects;

public class ProjectResponse
{
    public Guid Id { get; set; }

    public string ProjectCode { get; set; } = "";

    public string Name { get; set; } = "";

    public Guid ConstructionSiteId { get; set; }

    public decimal ContractValue { get; set; }

    public string ClientName { get; set; } = "";
}