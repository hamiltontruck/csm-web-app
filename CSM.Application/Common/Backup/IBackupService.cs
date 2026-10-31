namespace CSM.Application.Common.Backup;

public interface IBackupService
{
    Task EnsureAuthorizedAsync(Guid actorId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<BackupInfoDto>> GetBackupsAsync(
        Guid actorId,
        CancellationToken cancellationToken);

    Task<string> CreateBackupAsync(
        Guid actorId,
        CancellationToken cancellationToken);

    Task RestoreBackupAsync(
        Guid actorId,
        string backupFilePath,
        CancellationToken cancellationToken);
}