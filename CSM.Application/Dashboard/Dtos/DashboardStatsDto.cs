namespace CSM.Application.Dashboard.Dtos;

public sealed class DashboardStatsDto
{
    public int ActiveProjects { get; set; }

    public int TotalSites { get; set; }

    public int TotalEmployees { get; set; }

    public int TodayAttendance { get; set; }

    public int OpenWorkPermits { get; set; }

    public int LowStockItems { get; set; }

    public int BackupCount { get; set; }

    public DateTime? LastBackupUtc { get; set; }
}