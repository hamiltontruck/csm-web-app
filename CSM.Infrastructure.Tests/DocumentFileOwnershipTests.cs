using CSM.Application.Common.Exceptions;
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
    [InlineData(false)]
    [InlineData(true)]
    public async Task Download_rejects_a_foreign_file_registered_through_an_owned_parent(bool alias)
    {
        using var f = new Fixture();
        var foreign = new Document { CompanyId = Guid.NewGuid(), Name = "Foreign", DocumentNumber = "B", FileName = "foreign.txt", StoragePath = f.Path };
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

    private sealed class Fixture : IDisposable
    {
        public ApplicationDbContext Db { get; } = new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        public User Actor { get; }
        public Document Document { get; }
        public DocumentService Documents { get; }
        public DocumentVersionService Versions { get; }
        public string Path { get; } = "/uploads/documents/ownership-test-" + Guid.NewGuid().ToString("N") + ".txt";
        public string PhysicalPath => System.IO.Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", Path.TrimStart('/'));
        public Fixture()
        {
            var company = Guid.NewGuid();
            Actor = new() { CompanyId = company, IsActive = true, UserRoles = [new() { Role = new() { Name = "CompanyAdmin", GranularPermissions = [new() { Permission = GranularPermission.Compliance_View }, new() { Permission = GranularPermission.Compliance_Manage }] } }] };
            Document = new() { CompanyId = company, Name = "Owned", DocumentNumber = "A", FileName = "owned.txt", StoragePath = "/uploads/documents/metadata-only.txt" };
            Db.Users.Add(Actor); Db.Documents.Add(Document); Db.SaveChanges(true);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PhysicalPath)!);
            File.WriteAllText(PhysicalPath, "synthetic secret");
            Documents = new(Db); Versions = new(Db, Documents);
        }
        public void Dispose() { File.Delete(PhysicalPath); Db.Dispose(); }
    }
}
