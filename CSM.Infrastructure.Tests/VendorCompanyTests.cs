using System.Text.Json;
using CSM.Application.Common.Exceptions;
using CSM.Application.Procurement.Vendors.Dtos;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Finance;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class VendorCompanyTests
{
    [Fact]
    public async Task Companyless_admin_can_create_vendor_for_selected_company()
    {
        using var db = Database(true);
        var result = await new VendorService(db).CreateAsync(ActorId, Request(OtherCompany));
        Assert.Equal(OtherCompany, result.CompanyId);
        Assert.Equal(OtherCompany, db.Vendors.Single().CompanyId);
    }

    [Fact]
    public async Task Ordinary_user_cannot_select_other_company()
    {
        using var db = Database(false);
        await Assert.ThrowsAsync<VendorManagementException>(() =>
            new VendorService(db).CreateAsync(ActorId, Request(OtherCompany)));
        Assert.Empty(db.Vendors);
    }

    [Fact]
    public async Task Ordinary_user_defaults_to_own_company()
    {
        using var db = Database(false);
        var result = await new VendorService(db).CreateAsync(ActorId, Request(null));
        Assert.Equal(OwnCompany, result.CompanyId);
    }

    [Fact]
    public async Task Unknown_company_is_rejected()
    {
        using var db = Database(true);
        await Assert.ThrowsAsync<VendorManagementException>(() =>
            new VendorService(db).CreateAsync(ActorId, Request(Guid.NewGuid())));
        Assert.Empty(db.Vendors);
    }

    [Fact]
    public async Task Inactive_company_is_rejected()
    {
        using var db = Database(true);
        db.Companies.Single(x => x.Id == OtherCompany).IsActive = false;
        db.SaveChanges(acceptAllChangesOnSuccess: true);
        await Assert.ThrowsAsync<VendorManagementException>(() =>
            new VendorService(db).CreateAsync(ActorId, Request(OtherCompany)));
        Assert.Empty(db.Vendors);
    }

    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly Guid OwnCompany = Guid.NewGuid();
    private static readonly Guid OtherCompany = Guid.NewGuid();

    private static CreateVendorRequest Request(Guid? companyId) =>
        JsonSerializer.Deserialize<CreateVendorRequest>(JsonSerializer.Serialize(new
        {
            CompanyId = companyId, VendorCode = "VEN-001", Name = "Test Cement Supplier"
        }))!;

    private static TestDatabase Database(bool admin)
    {
        var db = new TestDatabase(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Companies.AddRange(new Company { Id = OwnCompany, Code = "OWN", Name = "Own" },
            new Company { Id = OtherCompany, Code = "OTHER", Name = "Other" });
        db.Users.Add(new User
        {
            Id = ActorId, IsActive = true, CompanyId = admin ? null : OwnCompany,
            UserRoles = [new UserRole { Id = Guid.NewGuid(), Role = new Role
            {
                Id = Guid.NewGuid(), Name = admin ? AppRoles.SuperAdmin : AppRoles.CompanyAdmin
            }}]
        });
        db.SaveChanges(acceptAllChangesOnSuccess: true);
        return db;
    }

    private sealed class TestDatabase(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
    {
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            base.SaveChangesAsync(acceptAllChangesOnSuccess: true, cancellationToken);
    }
}
