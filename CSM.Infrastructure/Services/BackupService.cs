using CSM.Application.Common.Backup;
using CSM.Domain.Constants;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Diagnostics;

namespace CSM.Infrastructure.Services;

public sealed class BackupService : IBackupService
{
    private readonly IConfiguration _configuration;
    private readonly ApplicationDbContext _context;

    public BackupService(IConfiguration configuration, ApplicationDbContext context)
    {
        _configuration = configuration;
        _context = context;
    }

    public async Task EnsureAuthorizedAsync(Guid actorId, CancellationToken cancellationToken)
    {
        // These operations affect the entire database. Check live authority before
        // touching configuration, the filesystem, or PostgreSQL client processes.
        if (!await _context.UserRoles.AsNoTracking().AnyAsync(x =>
                x.UserId == actorId && !x.IsDeleted &&
                x.User.IsActive && !x.User.IsDeleted && !x.Role.IsDeleted &&
                x.Role.Name == AppRoles.SuperAdmin, cancellationToken))
        {
            throw new UnauthorizedAccessException("You are not authorized to manage database backups.");
        }
    }

    private string BackupFolder =>
        Path.Combine(AppContext.BaseDirectory, "Backups");

    public async Task<IReadOnlyCollection<BackupInfoDto>> GetBackupsAsync(
        Guid actorId,
        CancellationToken cancellationToken)
    {
        await EnsureAuthorizedAsync(actorId, cancellationToken);
        Directory.CreateDirectory(BackupFolder);

        var files = Directory.GetFiles(BackupFolder, "*.dump")
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
        Guid actorId,
        CancellationToken cancellationToken)
    {
        await EnsureAuthorizedAsync(actorId, cancellationToken);
        Directory.CreateDirectory(BackupFolder);

        var fileName =
            $"CSM_{DateTime.UtcNow:yyyyMMdd_HHmmss}.dump";

        var path = Path.Combine(BackupFolder, fileName);
        await RunPostgresToolAsync("pg_dump", path, cancellationToken);

        return fileName;
    }

    public async Task RestoreBackupAsync(
        Guid actorId,
        string backupFilePath,
        CancellationToken cancellationToken)
    {
        await EnsureAuthorizedAsync(actorId, cancellationToken);
        var safePath = Path.GetFullPath(backupFilePath);
        var safeFolder = Path.GetFullPath(BackupFolder) + Path.DirectorySeparatorChar;
        if (!safePath.StartsWith(safeFolder, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The backup file must be inside the backups folder.");
        }

        await RunPostgresToolAsync("pg_restore", safePath, cancellationToken);
    }

    private async Task RunPostgresToolAsync(
        string toolName,
        string backupPath,
        CancellationToken cancellationToken)
    {
        var rawConnectionString =
            _configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(rawConnectionString))
        {
            throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");
        }

        var connection = new NpgsqlConnectionStringBuilder(rawConnectionString);
        var configuredToolPath = _configuration["Database:PostgresToolsPath"];
        var startInfo = new ProcessStartInfo
        {
            FileName = Path.Combine(configuredToolPath ?? string.Empty, toolName),
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };

        if (toolName == "pg_dump")
        {
            startInfo.ArgumentList.Add("--format=custom");
            startInfo.ArgumentList.Add("--no-owner");
            startInfo.ArgumentList.Add("--no-privileges");
            startInfo.ArgumentList.Add("--file");
            startInfo.ArgumentList.Add(backupPath);
        }
        else
        {
            startInfo.ArgumentList.Add("--clean");
            startInfo.ArgumentList.Add("--if-exists");
            startInfo.ArgumentList.Add("--no-owner");
            startInfo.ArgumentList.Add("--no-privileges");
            startInfo.ArgumentList.Add("--dbname");
            startInfo.ArgumentList.Add(connection.Database ?? string.Empty);
            startInfo.ArgumentList.Add(backupPath);
        }

        startInfo.Environment["PGHOST"] = connection.Host;
        startInfo.Environment["PGPORT"] = connection.Port.ToString();
        startInfo.Environment["PGDATABASE"] = connection.Database;
        startInfo.Environment["PGUSER"] = connection.Username;
        startInfo.Environment["PGPASSWORD"] = connection.Password;
        startInfo.Environment["PGSSLMODE"] = connection.SslMode == SslMode.Disable
            ? "disable"
            : "require";

        try
        {
            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException($"Could not start {toolName}.");
            var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            var error = await standardErrorTask;
            _ = await standardOutputTask;
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"{toolName} failed with exit code {process.ExitCode}: {error.Trim()}");
            }
        }
        catch (System.ComponentModel.Win32Exception exception)
        {
            throw new InvalidOperationException(
                "PostgreSQL client tools (pg_dump and pg_restore) are required for database backups. " +
                "Install them on the API host or set Database:PostgresToolsPath.",
                exception);
        }
    }
}
