namespace CSM.Application.Common.Backup;

public sealed class BackupInfoDto
{
    public string FileName { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public long FileSizeBytes { get; set; }
}