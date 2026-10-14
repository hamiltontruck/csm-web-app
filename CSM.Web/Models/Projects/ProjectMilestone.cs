using System.Text.Json.Serialization;

namespace CSM.Web.Models.Projects;

public class ProjectMilestone
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }

    public string MilestoneNumber { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }

    public DateOnly PlannedDate { get; set; }
    public DateOnly? ActualDate { get; set; }

    [JsonConverter(typeof(FlexibleMilestoneStatusConverter))]
    public MilestoneStatus Status { get; set; }

    public decimal ProgressPercentage { get; set; }

    public string? Notes { get; set; }
}