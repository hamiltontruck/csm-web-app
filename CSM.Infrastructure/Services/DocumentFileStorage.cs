using CSM.Application.Common.Exceptions;
using CSM.Domain.Entities.Compliance;
using CSM.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

/// <summary>Resolves document storage only; client paths never establish file ownership.</summary>
internal sealed class DocumentFileStorage(IWebHostEnvironment? environment)
{
    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    private string ContentRoot => environment?.ContentRootPath ?? Directory.GetCurrentDirectory();
    private string WebRoot => environment?.WebRootPath is { Length: > 0 } webRoot ? webRoot : Path.Combine(ContentRoot, "wwwroot");
    private string PrivateRoot => Path.Combine(ContentRoot, "App_Data", "documents");

    internal (string StoragePath, string PhysicalPath) NewFile(Document document)
    {
        var path = $"/private/documents/{document.CompanyId:D}/{document.Id:D}/{Guid.NewGuid():N}.bin";
        return (path, Resolve(path, document.CompanyId, document.Id));
    }

    internal string Resolve(string storagePath, Guid companyId, Guid documentId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(storagePath)) throw Unavailable();
            var relative = storagePath.Trim().Replace('\\', '/').TrimStart('/');
            string fullPath;
            string approvedRoot;
            if (relative.StartsWith("private/documents/", PathComparison))
            {
                fullPath = Path.GetFullPath(Path.Combine(PrivateRoot, relative["private/documents/".Length..]));
                approvedRoot = Path.Combine(PrivateRoot, companyId.ToString("D"), documentId.ToString("D"));
            }
            else
            {
                fullPath = Path.GetFullPath(Path.Combine(WebRoot, relative));
                approvedRoot = Path.Combine(WebRoot, "uploads", "documents");
            }
            if (!fullPath.StartsWith(Path.GetFullPath(approvedRoot) + Path.DirectorySeparatorChar, PathComparison))
                throw Unavailable();
            return fullPath;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw Unavailable();
        }
    }

    // Reject links on the entire physical path, including storage roots. Filesystem aliases
    // must not defeat lexical ownership checks or redirect uploads outside private storage.
    internal static void EnsureNoLinks(string physicalPath)
    {
        try
        {
            for (var path = physicalPath; !string.IsNullOrEmpty(path); path = Path.GetDirectoryName(path))
            {
                if ((File.Exists(path) || Directory.Exists(path)) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                    throw Unavailable();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw Unavailable();
        }
    }

    internal async Task<string> OwnedFileAsync(ApplicationDbContext db, Document document, string storagePath, CancellationToken ct)
    {
        var physical = Resolve(storagePath, document.CompanyId, document.Id);
        // Legacy files have no tenant directory. Keep recorded ownership, including deleted
        // parents, and fail closed when registrations attribute one physical file to two tenants.
        var foreignRegistrations = await (
            from version in db.DocumentVersions.AsNoTracking()
            join owner in db.Documents.IgnoreQueryFilters().AsNoTracking() on version.DocumentId equals owner.Id
            where owner.CompanyId != document.CompanyId
            select new { version.StoragePath, owner.CompanyId, owner.Id }).ToListAsync(ct);
        foreach (var registration in foreignRegistrations)
        {
            string candidate;
            try { candidate = Resolve(registration.StoragePath, registration.CompanyId, registration.Id); }
            catch (DocumentManagementException) { continue; }
            if (string.Equals(candidate, physical, PathComparison)) throw Unavailable();
        }
        EnsureNoLinks(physical);
        if (!File.Exists(physical)) throw new DocumentManagementException("Document version file was not found.");
        return physical;
    }

    internal async Task<(string StoragePath, long Length)> WriteAsync(Document document, Stream input, CancellationToken ct)
    {
        var location = NewFile(document);
        try
        {
            EnsureNoLinks(location.PhysicalPath);
            Directory.CreateDirectory(Path.GetDirectoryName(location.PhysicalPath)!);
            EnsureNoLinks(location.PhysicalPath);
            await using var output = new FileStream(location.PhysicalPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, useAsync: true);
            await input.CopyToAsync(output, ct);
            return (location.StoragePath, output.Length);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new DocumentManagementException("The document file could not be uploaded. Please try again.");
        }
    }

    internal static Stream OpenRead(string physicalPath)
    {
        try { return new FileStream(physicalPath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, useAsync: true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { throw new DocumentManagementException("Document version file was not found."); }
    }

    internal bool SameFile(Document document, string left, string right)
    {
        try { return string.Equals(Resolve(left, document.CompanyId, document.Id), Resolve(right, document.CompanyId, document.Id), PathComparison); }
        catch (DocumentManagementException) { return false; }
    }

    internal static DocumentManagementException Unavailable() => new("Document file is unavailable. Upload a new file or contact an administrator.");
}
