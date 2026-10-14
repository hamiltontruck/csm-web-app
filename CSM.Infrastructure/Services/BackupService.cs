using CSM.Application.Common.Backup;
using CSM.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace CSM.Infrastructure.Services;

public sealed class BackupService : IBackupService
{
    private readonly IConfiguration _configuration;

    public BackupService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    private string BackupFolder =>
        Path.Combine(AppContext.BaseDirectory, "Backups");

    public async Task<IReadOnlyCollection<BackupInfoDto>> GetBackupsAsync(
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(BackupFolder);

        var files = Directory.GetFiles(BackupFolder, "*.bak")
            .Select(x => new FileInfo(x))
            .OrderByDescending(x => x.CreationTimeUtc)
            .Select(x => new BackupInfoDto
            {
                FileName = x.Name,
                CreatedAtUtc = x.CreationTimeUtc,
                FileSizeBytes = x.Length
            })
            .ToList();

        return await Task.FromResult(files);
    }

    public async Task<string> CreateBackupAsync(
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(BackupFolder);

        var builder = new SqlConnectionStringBuilder(
            _configuration.GetConnectionString("DefaultConnection"));

        var database = builder.InitialCatalog;

        var fileName =
            $"CSM_{DateTime.UtcNow:yyyyMMdd_HHmmss}.bak";

        var path = Path.Combine(BackupFolder, fileName);

        var sql = $"""
BACKUP DATABASE [{database}]
TO DISK = N'{path}'
WITH INIT, COMPRESSION;
""";

        await ExecuteSqlAsync(sql, cancellationToken);

        return fileName;
    }

    public async Task RestoreBackupAsync(
        string backupFilePath,
        CancellationToken cancellationToken)
    {
        var builder = new SqlConnectionStringBuilder(
            _configuration.GetConnectionString("DefaultConnection"));

        var database = builder.InitialCatalog;

        builder.InitialCatalog = "master";

        var sql = $"""
ALTER DATABASE [{database}]
SET SINGLE_USER WITH ROLLBACK IMMEDIATE;

RESTORE DATABASE [{database}]
FROM DISK = N'{backupFilePath}'
WITH REPLACE;

ALTER DATABASE [{database}]
SET MULTI_USER;
""";

        await using var connection =
            new SqlConnection(builder.ConnectionString);

        await connection.OpenAsync(cancellationToken);

        await using var command =
            new SqlCommand(sql, connection);

        command.CommandTimeout = 0;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task ExecuteSqlAsync(
        string sql,
        CancellationToken cancellationToken)
    {
        await using var connection =
            new SqlConnection(
                _configuration.GetConnectionString("DefaultConnection"));

        await connection.OpenAsync(cancellationToken);

        await using var command =
            new SqlCommand(sql, connection);

        command.CommandTimeout = 0;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}