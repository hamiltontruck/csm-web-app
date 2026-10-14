using System.Text.Json.Serialization;

namespace CSM.Web.Models.Projects;

public class ProjectSummary
{
    public Guid Id { get; set; }

    [JsonPropertyName("projectCode")]
    public string Code { get; set; } = "";

    public string Name { get; set; } = "";

    [JsonPropertyName("constructionSiteId")]
    public Guid ConstructionSiteId { get; set; }

    public string ConstructionSiteName { get; set; } = "";

    public string Status { get; set; } = "";

    public decimal ProgressPercentage { get; set; }

    [JsonPropertyName("contractValue")]
    public decimal ContractValue { get; set; }

    public DateOnly? PlannedStartDate { get; set; }

    public DateOnly? PlannedEndDate { get; set; }
}