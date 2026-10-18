using System.Text.Json;
using CSM.Application.Common.Exceptions;
using CSM.Application.Procurement.Materials.Dtos;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Finance;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Infrastructure.Persistence;
using CSM.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CSM.Infrastructure.Tests;

public sealed class MaterialCompanyTests
{
    [Fact]
    public async Task Companyless_admin_can_create_material_for_selected_company()
    {
        using var db = Database(true);
        var result = await new MaterialService(db).CreateAsync(ActorId, Request(OtherCompany));
        Assert.Equal(OtherCompany, result.CompanyId);
        Assert.Equal(OtherCompany, db.Materials.Single().CompanyId);
    }

    [Fact]
    public async Task Ordinary_user_cannot_select_other_company()
    {
        using var db = Database(false);
        await Assert.ThrowsAsync<MaterialManagementException>(() =>
            new MaterialService(db).CreateAsync(ActorId, Request(OtherCompany)));
        Assert.Empty(db.Materials);
    }

    [Fact]
    public async Task Ordinary_user_defaults_to_own_company()
    {
        using var db = Database(false);
        var result = await new MaterialService(db).CreateAsync(ActorId, Request(null));
        Assert.Equal(OwnCompany, result.CompanyId);
    }

    [Fact]
    public async Task Unknown_company_is_rejected()
    {
        using var db = Database(true);
        await Assert.ThrowsAsync<MaterialManagementException>(() =>
            new MaterialService(db).CreateAsync(ActorId, Request(Guid.NewGuid())));
        Assert.Empty(db.Materials);
    }

    [Fact]
    public async Task Inactive_company_is_rejected()
    {
        using var db = Database(true);
        db.Companies.Single(x => x.Id == OtherCompany).IsActive = false;
        db.SaveChanges(acceptAllChangesOnSuccess: true);
        await Assert.ThrowsAsync<MaterialManagementException>(() =>
            new MaterialService(db).CreateAsync(ActorId, Request(OtherCompany)));
        Assert.Empty(db.Materials);
    }

    [Fact]
    public async Task Default_cost_code_from_other_company_is_rejected()
    {
        using var db = Database(true);
        var code = new CostCode { Id = Guid.NewGuid(), CompanyId = OwnCompany, Code = "COST", Name = "Other" };
        db.CostCodes.Add(code);
        db.SaveChanges(acceptAllChangesOnSuccess: true);
        var request = Request(OtherCompany);
        request.DefaultCostCodeId = code.Id;
        await Assert.ThrowsAsync<MaterialManagementException>(() =>
            new MaterialService(db).CreateAsync(ActorId, request));
        Assert.Empty(db.Materials);
    }

    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly Guid OwnCompany = Guid.NewGuid();
    private static readonly Guid OtherCompany = Guid.NewGuid();

    private static CreateMaterialRequest Request(Guid? companyId) =>
        JsonSerializer.Deserialize<CreateMaterialRequest>(JsonSerializer.Serialize(new
        {
            CompanyId = companyId, MaterialCode = "MAT-001", Name = "Cement",
            Category = "Cement", UnitOfMeasure = "Bag", StandardUnitCost = 1000m
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
