using CSM.Domain.Common;

namespace CSM.Domain.Entities.Projects;

public sealed class DailyProgressPhoto : TenantEntity
{
    public Guid DailySiteDiaryId { get; set; }

    public DailySiteDiary DailySiteDiary { get; set; } = null!;

    public string FileName { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public string? Caption { get; set; }

    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;

    public Guid UploadedByUserId { get; set; }
}