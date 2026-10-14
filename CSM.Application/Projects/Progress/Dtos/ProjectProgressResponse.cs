namespace CSM.Application.Projects.Progress.Dtos;

public sealed class ProjectProgressResponse
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;

    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int InProgressTasks { get; set; }
    public int NotStartedTasks { get; set; }
    public int OverdueTasks { get; set; }

    public decimal OverallProgress { get; set; }

    public decimal EstimatedHours { get; set; }
    public decimal ActualHours { get; set; }

    public string HealthStatus { get; set; } = "OnSchedule";
}