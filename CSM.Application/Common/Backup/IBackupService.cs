namespace CSM.Application.Common.Backup;

public interface IBackupService
{
    Task<IReadOnlyCollection<BackupInfoDto>> GetBackupsAsync(
        CancellationToken cancellationToken);

    Task<string> CreateBackupAsync(
        CancellationToken cancellationToken);

    Task RestoreBackupAsync(
        string backupFilePath,
        CancellationToken cancellationToken);
}