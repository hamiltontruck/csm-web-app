using System.Text.Json.Serialization;

namespace CSM.Web.Models.Projects;

public class ProjectDetail
{
    public Guid Id { get; set; }

    [JsonPropertyName("constructionSiteId")]
    public Guid SiteId { get; set; }

    public string Name { get; set; } = "";

    [JsonPropertyName("projectCode")]
    public string Code { get; set; } = "";

    public string ConstructionSiteName { get; set; } = "";

    public string Status { get; set; } = "";

    public decimal ProgressPercentage { get; set; }

    public DateOnly? PlannedStartDate { get; set; }

    public DateOnly? PlannedEndDate { get; set; }

    [JsonPropertyName("contractValue")]
    public decimal Budget { get; set; }

    public decimal Expenses { get; set; }

    public string ProjectManager { get; set; } = "";

    public string Description { get; set; } = "";
}