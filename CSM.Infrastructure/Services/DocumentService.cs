using CSM.Application.Common.Exceptions;
using CSM.Application.Common.Security;
using CSM.Application.Compliance.Documents;
using CSM.Application.Compliance.Documents.Dtos;
using CSM.Domain.Constants;
using CSM.Domain.Enums;
using Microsoft.AspNetCore.Hosting;
using CSM.Domain.Entities.Compliance;
using CSM.Domain.Entities.Identity;
using CSM.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public sealed class DocumentService : IDocumentService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly DocumentFileStorage _files;

    public DocumentService(ApplicationDbContext dbContext, IWebHostEnvironment? environment = null)
    {
        _dbContext = dbContext;
        _files = new(environment);
    }

    public async Task<IReadOnlyCollection<DocumentResponse>> GetAllAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(currentUserId, cancellationToken);

        EnsureCanManage(actor);

        var query = _dbContext.Documents
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .AsQueryable();

        if (!IsSuperAdmin(actor))
        {
            query = query.Where(x => x.CompanyId == GetActorCompanyId(actor));
        }

        return await (
            from d in query
            join s in _dbContext.ConstructionSites
                on d.ConstructionSiteId equals s.Id into sites
            from site in sites.DefaultIfEmpty()
            orderby d.CreatedAtUtc descending
            select new DocumentResponse
            {
                Id = d.Id,
                CompanyId = d.CompanyId,

                ConstructionSiteId = d.ConstructionSiteId,
                ConstructionSiteName = site != null && site.CompanyId == d.CompanyId ? site.Name : null,

                DocumentNumber = d.DocumentNumber,
                Name = d.Name,
                DocumentType = d.DocumentType,

                DrawingNumber = d.DrawingNumber,
                SheetNumber = d.SheetNumber,
                Discipline = d.Discipline,
                DrawingStatus = d.DrawingStatus,
                CurrentRevision = d.CurrentRevision,

                FileName = d.FileName,
                StoragePath = d.StoragePath,
                ContentType = d.ContentType,
                FileSizeBytes = d.FileSizeBytes,

                ProjectId = d.ProjectId,
                EmployeeId = d.EmployeeId,
                RelatedEntityId = d.RelatedEntityId,
                RelatedEntityType = d.RelatedEntityType,

                IssueDate = d.IssueDate,
                ExpiryDate = d.ExpiryDate,
                IsConfidential = d.IsConfidential,
                Description = d.Description,

                CreatedAtUtc = d.CreatedAtUtc,
                UpdatedAtUtc = d.UpdatedAtUtc
            }).ToListAsync(cancellationToken);
    }

    public async Task<DocumentResponse> GetByIdAsync(
        Guid currentUserId,
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(currentUserId, cancellationToken);

        EnsureCanManage(actor);

        var document = await _dbContext.Documents

            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == documentId && !x.IsDeleted,
                cancellationToken)
            ?? throw new DocumentManagementException("Document was not found.");

        EnsureCompanyAccess(actor, document.CompanyId);

        return Map(document);
    }

    public async Task<DocumentResponse> CreateAsync(
        Guid currentUserId,
        CreateDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await RequireAccessAsync(_dbContext, currentUserId, true, cancellationToken);

        EnsureCanManage(actor);

        var companyId = GetActorCompanyId(actor);

        Validate(
            request.DocumentNumber,
            request.Name,
            request.FileName,
            request.StoragePath,
            request.FileSizeBytes,
            request.IssueDate,
            request.ExpiryDate);

        await ValidateParentsAsync(companyId, request.ConstructionSiteId, request.ProjectId, request.EmployeeId, cancellationToken);

        var normalizedNumber = request.DocumentNumber.Trim();

        var duplicate = await _dbContext.Documents.AnyAsync(
            x =>
                !x.IsDeleted &&
                x.CompanyId == companyId &&
                x.DocumentNumber == normalizedNumber,
            cancellationToken);

        if (duplicate)
        {
            throw new DocumentManagementException(
                "A document with this document number already exists in the company.");
        }

        var document = new Document
        {
            CompanyId = companyId,
            DocumentNumber = normalizedNumber,
            Name = request.Name.Trim(),
            DocumentType = request.DocumentType,

            DrawingNumber = request.DrawingNumber,
            SheetNumber = request.SheetNumber,
            Discipline = request.Discipline,
            DrawingStatus = request.DrawingStatus,
            CurrentRevision = request.CurrentRevision,

            FileName = request.FileName.Trim(),
            StoragePath = string.Empty,
            ContentType = Clean(request.ContentType),
            FileSizeBytes = request.FileSizeBytes,

            ConstructionSiteId = request.ConstructionSiteId,
            ProjectId = request.ProjectId,
            EmployeeId = request.EmployeeId,
            RelatedEntityId = request.RelatedEntityId,
            RelatedEntityType = Clean(request.RelatedEntityType),

            IssueDate = request.IssueDate,
            ExpiryDate = request.ExpiryDate,

            IsConfidential = request.IsConfidential,
            Description = Clean(request.Description)
        };

        _dbContext.Documents.Add(document);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var created = await _dbContext.Documents

            .AsNoTracking()
            .SingleAsync(
                x => x.Id == document.Id,
                cancellationToken);

        return Map(created);
    }

    public async Task<DocumentResponse> UpdateAsync(
        Guid currentUserId,
        Guid documentId,
        UpdateDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await RequireAccessAsync(_dbContext, currentUserId, true, cancellationToken);

        EnsureCanManage(actor);

        var document = await _dbContext.Documents
            .SingleOrDefaultAsync(
                x => x.Id == documentId && !x.IsDeleted,
                cancellationToken)
            ?? throw new DocumentManagementException("Document was not found.");

        EnsureCompanyAccess(actor, document.CompanyId);

        Validate(
            request.DocumentNumber,
            request.Name,
            request.FileName,
            request.StoragePath,
            request.FileSizeBytes,
            request.IssueDate,
            request.ExpiryDate);

        await ValidateParentsAsync(document.CompanyId, request.ConstructionSiteId, request.ProjectId, request.EmployeeId, cancellationToken);

        var normalizedNumber = request.DocumentNumber.Trim();

        var duplicate = await _dbContext.Documents.AnyAsync(
            x =>
                x.Id != document.Id &&
                !x.IsDeleted &&
                x.CompanyId == document.CompanyId &&
                x.DocumentNumber == normalizedNumber,
            cancellationToken);

        if (duplicate)
        {
            throw new DocumentManagementException(
                "A document with this document number already exists in the company.");
        }

        document.DocumentNumber = normalizedNumber;
        document.Name = request.Name.Trim();
        document.DocumentType = request.DocumentType;

        document.DrawingNumber = request.DrawingNumber;
        document.SheetNumber = request.SheetNumber;
        document.Discipline = request.Discipline;
        document.DrawingStatus = request.DrawingStatus;
        document.CurrentRevision = request.CurrentRevision;

        // File metadata and location are changed only by an authorized upload.
        document.ConstructionSiteId = request.ConstructionSiteId;

        document.ProjectId = request.ProjectId;
        document.EmployeeId = request.EmployeeId;
        document.RelatedEntityId = request.RelatedEntityId;
        document.RelatedEntityType = Clean(request.RelatedEntityType);

        document.IssueDate = request.IssueDate;
        document.ExpiryDate = request.ExpiryDate;

        document.IsConfidential = request.IsConfidential;
        document.Description = Clean(request.Description);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var updated = await _dbContext.Documents

            .AsNoTracking()
            .SingleAsync(
                x => x.Id == document.Id,
                cancellationToken);

        return Map(updated);
    }


    public async Task<DocumentVersionResponse> UploadVersionAsync(
        Guid currentUserId,
        Guid documentId,
        Stream fileStream,
        string fileName,
        string? contentType,
        long fileSizeBytes,
        string? revisionNotes,
        CancellationToken cancellationToken = default)
    {
        var actor = await RequireAccessAsync(_dbContext, currentUserId, true, cancellationToken);

        EnsureCanManage(actor);

        var document = await _dbContext.Documents
            .SingleOrDefaultAsync(
                x => x.Id == documentId && !x.IsDeleted,
                cancellationToken)
            ?? throw new DocumentManagementException("Document not found.");

        EnsureCompanyAccess(actor, document.CompanyId);

        var uploadName = Path.GetFileName(fileName.Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(uploadName) || uploadName.Length > 255 || fileSizeBytes < 0)
            throw new DocumentManagementException("A valid file name and size are required.");
        var location = await _files.WriteAsync(document, fileStream, cancellationToken);

        var nextVersion = await _dbContext.DocumentVersions
            .Where(x => x.DocumentId == documentId)
            .MaxAsync(x => (int?)x.VersionNumber, cancellationToken) ?? 0;
        nextVersion++;

        var currentVersions = await _dbContext.DocumentVersions
            .Where(x => x.DocumentId == documentId && x.IsCurrent)
            .ToListAsync(cancellationToken);

        foreach (var v in currentVersions)
            v.IsCurrent = false;

        var version = new DocumentVersion
        {
            DocumentId = documentId,
            VersionNumber = nextVersion,
            FileName = uploadName,
            StoragePath = location.StoragePath,
            UploadedByUserId = currentUserId,
            ContentType = contentType,
            FileSizeBytes = location.Length,
            RevisionNotes = revisionNotes,
            IsCurrent = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.DocumentVersions.Add(version);

        document.FileName = version.FileName;
        document.StoragePath = version.StoragePath;
        document.ContentType = version.ContentType;
        document.FileSizeBytes = version.FileSizeBytes;
        document.CurrentRevision = version.VersionNumber.ToString();
        document.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new DocumentVersionResponse
        {
            Id = version.Id,
            DocumentId = version.DocumentId,
            UploadedByUserId = version.UploadedByUserId,
            VersionNumber = version.VersionNumber,
            FileName = version.FileName,
            StoragePath = version.StoragePath,
            ContentType = version.ContentType,
            FileSizeBytes = version.FileSizeBytes,
            RevisionNotes = version.RevisionNotes,
            IsCurrent = version.IsCurrent,
            CreatedAtUtc = version.CreatedAtUtc
        };
    }

    private async Task<User> GetActorAsync(
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        return await RequireAccessAsync(_dbContext, currentUserId, false, cancellationToken);
    }

    internal static async Task<User> RequireAccessAsync(ApplicationDbContext db, Guid actorId, bool manage, CancellationToken ct)
    {
        var actor = await db.Users.AsNoTracking().Include(x => x.UserRoles).ThenInclude(x => x.Role).ThenInclude(x => x.GranularPermissions)
            .SingleOrDefaultAsync(x => x.Id == actorId && x.IsActive && !x.IsDeleted, ct);
        if (actor is null) throw new DocumentManagementException("You are not authorized to access this document.");
        EnsureCanManage(actor);
        var grants = actor.UserRoles.Where(x => !x.IsDeleted && !x.Role.IsDeleted).SelectMany(x => x.Role.GranularPermissions).Select(x => x.Permission).ToHashSet();
        if (!grants.Contains(GranularPermission.Compliance_View) || (manage && !grants.Contains(GranularPermission.Compliance_Manage)))
            throw new DocumentManagementException("You are not authorized to access this document.");
        return actor;
    }

    private async Task ValidateParentsAsync(Guid companyId, Guid? siteId, Guid? projectId, Guid? employeeId, CancellationToken ct)
    {
        if (siteId.HasValue && !await _dbContext.ConstructionSites.AnyAsync(x => x.Id == siteId.Value && x.CompanyId == companyId && !x.IsDeleted, ct))
            throw new DocumentManagementException("The selected construction site is unavailable.");
        if (projectId.HasValue && !await _dbContext.Projects.AnyAsync(x => x.Id == projectId.Value && x.CompanyId == companyId && !x.IsDeleted && (!siteId.HasValue || x.ConstructionSiteId == siteId.Value), ct))
            throw new DocumentManagementException("The selected project is unavailable.");
        if (employeeId.HasValue && !await _dbContext.Employees.AnyAsync(x => x.Id == employeeId.Value && x.CompanyId == companyId && !x.IsDeleted, ct))
            throw new DocumentManagementException("The selected employee is unavailable.");
    }

    private static void EnsureCanManage(User actor)
    {
        if (IsSuperAdmin(actor) ||
            HasRole(actor, AppRoles.CompanyAdmin) ||
            HasRole(actor, AppRoles.SiteManager))
        {
            return;
        }

        throw new DocumentManagementException(
            "You are not authorized to manage documents.");
    }

    private static void EnsureCompanyAccess(User actor, Guid companyId)
    {
        if (IsSuperAdmin(actor))
            return;

        if (!actor.CompanyId.HasValue ||
            actor.CompanyId.Value != companyId)
        {
            throw new DocumentManagementException(
                "You are not authorized to access this document.");
        }
    }

    private static Guid GetActorCompanyId(User actor)
    {
        if (!actor.CompanyId.HasValue)
        {
            throw new DocumentManagementException(
                "Current user is not assigned to a company.");
        }

        return actor.CompanyId.Value;
    }

    private static bool IsSuperAdmin(User actor)
        => HasRole(actor, AppRoles.SuperAdmin);

    private static bool HasRole(User actor, string roleName)
    {
        return actor.UserRoles.Any(
            x =>
                !x.IsDeleted &&
                !x.Role.IsDeleted &&
                string.Equals(
                    x.Role.Name,
                    roleName,
                    StringComparison.OrdinalIgnoreCase));
    }

    private static void Validate(
        string documentNumber,
        string name,
        string fileName,
        string storagePath,
        long fileSizeBytes,
        DateOnly? issueDate,
        DateOnly? expiryDate)
    {
        if (string.IsNullOrWhiteSpace(documentNumber))
            throw new DocumentManagementException("Document number is required.");

        if (documentNumber.Trim().Length > 50)
            throw new DocumentManagementException("Document number cannot exceed 50 characters.");

        if (string.IsNullOrWhiteSpace(name))
            throw new DocumentManagementException("Document name is required.");

        if (name.Trim().Length > 250)
            throw new DocumentManagementException("Document name cannot exceed 250 characters.");

        if (string.IsNullOrWhiteSpace(fileName))
            throw new DocumentManagementException("File name is required.");

        if (fileName.Trim().Length > 255)
            throw new DocumentManagementException("File name cannot exceed 255 characters.");

        if (storagePath is { Length: > 1000 })
            throw new DocumentManagementException("Storage path cannot exceed 1000 characters.");

        if (fileSizeBytes < 0)
            throw new DocumentManagementException("File size cannot be negative.");

        if (issueDate.HasValue &&
            expiryDate.HasValue &&
            expiryDate.Value < issueDate.Value)
        {
            throw new DocumentManagementException(
                "Expiry date cannot be earlier than issue date.");
        }
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static DocumentResponse Map(Document document)
    {
        return new DocumentResponse
        {
            Id = document.Id,
            CompanyId = document.CompanyId,
            ConstructionSiteName = null,
            DocumentNumber = document.DocumentNumber,
            Name = document.Name,
            DocumentType = document.DocumentType,

            DrawingNumber = document.DrawingNumber,
            SheetNumber = document.SheetNumber,
            Discipline = document.Discipline,
            DrawingStatus = document.DrawingStatus,
            CurrentRevision = document.CurrentRevision,

            FileName = document.FileName,
            StoragePath = document.StoragePath,
            ContentType = document.ContentType,
            FileSizeBytes = document.FileSizeBytes,

            ConstructionSiteId = document.ConstructionSiteId,
            ProjectId = document.ProjectId,
            EmployeeId = document.EmployeeId,
            RelatedEntityId = document.RelatedEntityId,
            RelatedEntityType = document.RelatedEntityType,

            IssueDate = document.IssueDate,
            ExpiryDate = document.ExpiryDate,
            IsConfidential = document.IsConfidential,
            Description = document.Description,

            CreatedAtUtc = document.CreatedAtUtc,
            UpdatedAtUtc = document.UpdatedAtUtc
        };
    }
}