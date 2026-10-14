using CSM.Application.Common.Backup;
using CSM.Application.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = AppPolicies.CompanyManagement)]
public sealed class BackupController : ControllerBase
{
    private readonly IBackupService _backupService;

    public BackupController(IBackupService backupService)
    {
        _backupService = backupService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<BackupInfoDto>>> GetBackups(
        CancellationToken cancellationToken)
    {
        var backups = await _backupService.GetBackupsAsync(cancellationToken);
        return Ok(backups);
    }

    [HttpPost("create")]
    public async Task<ActionResult<object>> CreateBackup(
        CancellationToken cancellationToken)
    {
        var fileName = await _backupService.CreateBackupAsync(cancellationToken);

        return Ok(new
        {
            message = "Backup created successfully.",
            fileName
        });
    }

    [HttpPost("restore")]
    [RequestSizeLimit(1024L * 1024 * 1024)]
    public async Task<IActionResult> RestoreBackup(
        IFormFile backupFile,
        CancellationToken cancellationToken)
    {
        if (backupFile == null || backupFile.Length == 0)
        {
            return BadRequest(new
            {
                message = "Please select a valid backup file."
            });
        }

        var backupFolder = Path.Combine(
            AppContext.BaseDirectory,
            "Backups");

        Directory.CreateDirectory(backupFolder);

        var filePath = Path.Combine(
            backupFolder,
            backupFile.FileName);

        using (var stream = System.IO.File.Create(filePath))
        {
            await backupFile.CopyToAsync(stream, cancellationToken);
        }

        await _backupService.RestoreBackupAsync(
            filePath,
            cancellationToken);

        return Ok(new
        {
            message = "Database restored successfully."
        });
    }
}