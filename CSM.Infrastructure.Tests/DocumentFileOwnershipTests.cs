using CSM.Application.Common.Exceptions;
using CSM.Application.Compliance.Documents.Dtos;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using CSM.Domain.Entities.Compliance;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class DocumentFileOwnershipTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Download_rejects_a_foreign_file_registered_through_an_owned_parent(bool alias, bool deleted)
    {
        using var f = new Fixture();
        var foreign = new Document { CompanyId = Guid.NewGuid(), IsDeleted = deleted, Name = "Foreign", DocumentNumber = "B", FileName = "foreign.txt", StoragePath = f.Path };
        f.Db.Documents.Add(foreign);
        f.Db.DocumentVersions.Add(new() { DocumentId = foreign.Id, StoragePath = f.Path, FileName = "foreign.txt", VersionNumber = 1 });
        var forged = new DocumentVersion { DocumentId = f.Document.Id, StoragePath = alias ? f.Path.Replace("/documents/", "/documents/../documents/") : f.Path, FileName = "claimed.txt", VersionNumber = 1 };
        f.Db.DocumentVersions.Add(forged);
        f.Db.SaveChanges(true);
        await Assert.ThrowsAsync<DocumentManagementException>(() => f.Versions.DownloadAsync(f.Actor.Id, f.Document.Id, forged.Id, default));
        Assert.Equal("synthetic secret", await File.ReadAllTextAsync(f.PhysicalPath));
    }

    [Fact]
    public async Task Read_denies_an_active_manager_without_the_current_view_grant()
    {
        using var f = new Fixture();
        f.Db.RoleGranularPermissions.RemoveRange(f.Db.RoleGranularPermissions);
        f.Db.SaveChanges(true);
        await Assert.ThrowsAsync<DocumentManagementException>(() => f.Documents.GetByIdAsync(f.Actor.Id, f.Document.Id));
    }

    [Fact]
    public async Task Read_denies_an_inactive_manager_even_with_permission()
    {
        using var f = new Fixture();
        f.Actor.IsActive = false;
        f.Db.SaveChanges(true);
        await Assert.ThrowsAsync<DocumentManagementException>(() => f.Documents.GetByIdAsync(f.Actor.Id, f.Document.Id));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Metadata_cannot_claim_foreign_or_unknown_files_or_clear_current_versions(bool foreign)
    {
        using var f = new Fixture();
        var current = f.Register("/uploads/documents/existing.txt");
        if (foreign)
        {
            var owner = new Document { CompanyId = Guid.NewGuid(), Name = "Foreign", DocumentNumber = "B" };
            f.Db.Documents.Add(owner);
            f.Db.DocumentVersions.Add(new() { DocumentId = owner.Id, StoragePath = f.Path, VersionNumber = 1 });
        }
        f.Db.SaveChanges(true);
        var count = await f.Db.DocumentVersions.CountAsync();
        await Assert.ThrowsAsync<DocumentManagementException>(() => f.Versions.CreateAsync(f.Actor.Id, f.Document.Id,
            new() { StoragePath = f.Path.Replace("/documents/", "/documents/../documents/"), FileName = "claimed.txt" }, default));
        Assert.Equal(count, await f.Db.DocumentVersions.CountAsync());
        Assert.True(current.IsCurrent);
    }

    [Fact]
    public async Task Metadata_cannot_claim_a_different_document_file_even_in_the_same_company()
    {
        using var f = new Fixture();
        var other = new Document { CompanyId = f.Document.CompanyId, Name = "Other", DocumentNumber = "OTHER" };
        f.Db.Documents.Add(other);
        f.Db.DocumentVersions.Add(new() { DocumentId = other.Id, StoragePath = f.Path, VersionNumber = 1 });
        f.Db.SaveChanges(true);
        await Assert.ThrowsAsync<DocumentManagementException>(() => f.Versions.CreateAsync(f.Actor.Id, f.Document.Id,
            new() { StoragePath = f.Path, FileName = "claim.txt" }, default));
        Assert.Empty(f.Db.DocumentVersions.Where(x => x.DocumentId == f.Document.Id));
    }

    [Fact]
    public async Task Existing_owned_legacy_file_download_and_metadata_reuse_preserve_authoritative_file_details()
    {
        using var f = new Fixture();
        var original = f.Register(f.Path);
        var result = await f.Versions.CreateAsync(f.Actor.Id, f.Document.Id, new() {
            StoragePath = f.Path.Replace("/documents/", "/documents/../documents/"), FileName = "forged-name.exe", ContentType = "wrong/type", FileSizeBytes = 999 }, default);
        Assert.Equal(original.FileName, result.FileName);
        Assert.Equal(original.ContentType, result.ContentType);
        Assert.Equal(original.FileSizeBytes, result.FileSizeBytes);
        Assert.Equal(f.Actor.Id, result.UploadedByUserId);
        Assert.False(original.IsCurrent);
        var downloaded = await f.Versions.DownloadAsync(f.Actor.Id, f.Document.Id, result.Id, default);
        using var reader = new StreamReader(downloaded.Stream);
        Assert.Equal("synthetic secret", await reader.ReadToEndAsync());
    }

    [Theory]
    [InlineData("/other/secret.txt")]
    [InlineData("/uploads/photos/secret.txt")]
    [InlineData("/uploads/documents/../../../secret.txt")]
    public async Task Download_rejects_paths_outside_the_document_storage_roots(string path)
    {
        using var f = new Fixture();
        var version = f.Register(path);
        var error = await Assert.ThrowsAsync<DocumentManagementException>(() => f.Versions.DownloadAsync(f.Actor.Id, f.Document.Id, version.Id, default));
        Assert.DoesNotContain(f.Environment.ContentRootPath, error.Message);
    }

    [Fact]
    public async Task Upload_registers_private_company_document_file_and_downloads_its_actual_bytes()
    {
        using var f = new Fixture();
        using var bytes = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        var uploaded = await f.Documents.UploadVersionAsync(f.Actor.Id, f.Document.Id, bytes, "report.pdf", "application/pdf", 999, null);
        Assert.StartsWith($"/private/documents/{f.Document.CompanyId:D}/{f.Document.Id:D}/", uploaded.StoragePath);
        Assert.Equal(f.Actor.Id, uploaded.UploadedByUserId);
        Assert.Equal(f.Document.Id, uploaded.DocumentId);
        Assert.Equal(4, uploaded.FileSizeBytes);
        Assert.Equal(uploaded.StoragePath, f.Document.StoragePath);
        Assert.Empty(Directory.EnumerateFiles(f.Environment.WebRootPath, "*", SearchOption.AllDirectories).Where(x => x != f.PhysicalPath));
        var downloaded = await f.Versions.DownloadAsync(f.Actor.Id, f.Document.Id, uploaded.Id, default);
        await using var stream = downloaded.Stream;
        using var copy = new MemoryStream(); await stream.CopyToAsync(copy);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, copy.ToArray());
    }

    [Fact]
    public async Task Missing_manage_grant_rejects_upload_and_metadata_changes_before_io_or_mutation()
    {
        using var f = new Fixture();
        var current = f.Register(f.Path);
        f.Db.RoleGranularPermissions.RemoveRange(f.Db.RoleGranularPermissions.Where(x => x.Permission == GranularPermission.Compliance_Manage));
        f.Db.SaveChanges(true);
        using var bytes = new MemoryStream(new byte[] { 1, 2, 3 });
        await Assert.ThrowsAsync<DocumentManagementException>(() => f.Documents.UploadVersionAsync(f.Actor.Id, f.Document.Id, bytes, "x.txt", "text/plain", 3, null));
        await Assert.ThrowsAsync<DocumentManagementException>(() => f.Versions.CreateAsync(f.Actor.Id, f.Document.Id, new() { StoragePath = f.Path }, default));
        Assert.Equal(0, bytes.Position);
        Assert.False(Directory.Exists(System.IO.Path.Combine(f.Environment.ContentRootPath, "App_Data")));
        Assert.Single(f.Db.DocumentVersions);
        Assert.True(current.IsCurrent);
    }

    [Theory]
    [InlineData("site")]
    [InlineData("project")]
    [InlineData("employee")]
    public async Task Metadata_create_and_update_reject_foreign_company_parent_links(string kind)
    {
        using var f = new Fixture();
        var foreign = Guid.NewGuid(); var id = Guid.NewGuid();
        if (kind == "site") f.Db.ConstructionSites.Add(new() { Id = id, CompanyId = foreign, Name = "Foreign" });
        if (kind == "project") f.Db.Projects.Add(new() { Id = id, CompanyId = foreign, Name = "Foreign" });
        if (kind == "employee") f.Db.Employees.Add(new() { Id = id, CompanyId = foreign, FirstName = "Foreign", EmployeeNumber = "F" });
        f.Db.SaveChanges(true);
        var create = new CreateDocumentRequest { DocumentNumber = "NEW", Name = "New", FileName = "new.txt", StoragePath = "metadata-only", ConstructionSiteId = kind == "site" ? id : null, ProjectId = kind == "project" ? id : null, EmployeeId = kind == "employee" ? id : null };
        var update = new UpdateDocumentRequest { DocumentNumber = "NEW", Name = "New", FileName = "new.txt", StoragePath = f.Document.StoragePath, ConstructionSiteId = create.ConstructionSiteId, ProjectId = create.ProjectId, EmployeeId = create.EmployeeId };
        await Assert.ThrowsAsync<DocumentManagementException>(() => f.Documents.CreateAsync(f.Actor.Id, create));
        await Assert.ThrowsAsync<DocumentManagementException>(() => f.Documents.UpdateAsync(f.Actor.Id, f.Document.Id, update));
        Assert.Single(f.Db.Documents);
        Assert.Equal("Owned", f.Document.Name);
    }

    [Fact]
    public async Task Download_rejects_a_symlink_even_when_the_legacy_path_is_registered()
    {
        if (OperatingSystem.IsWindows()) return;
        using var f = new Fixture();
        var aliasPath = f.PhysicalPath + ".link";
        File.CreateSymbolicLink(aliasPath, f.PhysicalPath);
        var version = f.Register(f.Path + ".link");
        await Assert.ThrowsAsync<DocumentManagementException>(() => f.Versions.DownloadAsync(f.Actor.Id, f.Document.Id, version.Id, default));
    }

    private sealed class Fixture : IDisposable
    {
        public ApplicationDbContext Db { get; } = new TestDb(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        public TestEnvironment Environment { get; } = new();
        public User Actor { get; }
        public Document Document { get; }
        public DocumentService Documents { get; }
        public DocumentVersionService Versions { get; }
        public string Path { get; } = "/uploads/documents/ownership-test-" + Guid.NewGuid().ToString("N") + ".txt";
        public string PhysicalPath => System.IO.Path.Combine(Environment.WebRootPath, Path.TrimStart('/'));
        public Fixture()
        {
            var company = Guid.NewGuid();
            Actor = new() { CompanyId = company, IsActive = true, UserRoles = [new() { Role = new() { Name = "CompanyAdmin", GranularPermissions = [new() { Permission = GranularPermission.Compliance_View }, new() { Permission = GranularPermission.Compliance_Manage }] } }] };
            Document = new() { CompanyId = company, Name = "Owned", DocumentNumber = "A", FileName = "owned.txt", StoragePath = "/uploads/documents/metadata-only.txt" };
            Db.Users.Add(Actor); Db.Documents.Add(Document); Db.SaveChanges(true);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PhysicalPath)!);
            File.WriteAllText(PhysicalPath, "synthetic secret");
            Documents = new(Db, Environment); Versions = new(Db, Documents, Environment);
        }
        public DocumentVersion Register(string path)
        {
            var version = new DocumentVersion { DocumentId = Document.Id, VersionNumber = 1, FileName = "original.txt", StoragePath = path, ContentType = "text/plain", FileSizeBytes = 16 };
            Db.DocumentVersions.Add(version); Db.SaveChanges(true); return version;
        }
        public void Dispose() { Db.Dispose(); Directory.Delete(Environment.ContentRootPath, true); }
    }

    private sealed class TestDb(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
    {
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => base.SaveChangesAsync(true, cancellationToken);
    }
    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string ContentRootPath { get; set; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "csm-document-tests-" + Guid.NewGuid().ToString("N"));
        public string WebRootPath { get; set; }
        public string ApplicationName { get; set; } = "Tests";
        public string EnvironmentName { get; set; } = "Testing";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public TestEnvironment() { WebRootPath = System.IO.Path.Combine(ContentRootPath, "wwwroot"); }
    }
}
