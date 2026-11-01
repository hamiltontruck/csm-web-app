using CSM.Application.Common.Backup;
using CSM.Application.Common.Security;
using CSM.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CSM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = AppPolicies.CompanyManagement)]
[Authorize(Roles = AppRoles.SuperAdmin)]
public sealed class BackupController : CurrentUserControllerBase
{
    private readonly IBackupService _backupService;

    public BackupController(IBackupService backupService)
    {
        _backupService = backupService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<BackupInfoDto>>> GetBackups(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _backupService.GetBackupsAsync(CurrentUserId, cancellationToken));
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPost("create")]
    public async Task<ActionResult<object>> CreateBackup(CancellationToken cancellationToken)
    {
        try
        {
            var fileName = await _backupService.CreateBackupAsync(CurrentUserId, cancellationToken);
            return Ok(new { message = "Backup created successfully.", fileName });
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPost("restore")]
    [RequestSizeLimit(1024L * 1024 * 1024)]
    public async Task<IActionResult> RestoreBackup(IFormFile backupFile, CancellationToken cancellationToken)
    {
        try
        {
            var actorId = CurrentUserId;
            // Recheck database authority before staging any uploaded file.
            await _backupService.EnsureAuthorizedAsync(actorId, cancellationToken);
            if (backupFile == null || backupFile.Length == 0)
                return BadRequest(new { message = "Please select a valid backup file." });
            if (!Path.GetExtension(backupFile.FileName).Equals(".dump", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = "Please select a PostgreSQL .dump backup file." });

            var backupFolder = Path.Combine(AppContext.BaseDirectory, "Backups");
            Directory.CreateDirectory(backupFolder);
            var filePath = Path.Combine(backupFolder, Path.GetFileName(backupFile.FileName));
            using (var stream = System.IO.File.Create(filePath))
                await backupFile.CopyToAsync(stream, cancellationToken);

            await _backupService.RestoreBackupAsync(actorId, filePath, cancellationToken);
            return Ok(new { message = "Database restored successfully." });
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
}
