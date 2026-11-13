using System.Text.Json;
using CSM.Application.Common.Exceptions;
using CSM.Application.Procurement.Equipment.Dtos;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Enums;
using CSM.Infrastructure.Services;
using Xunit;
namespace CSM.Infrastructure.Tests;

public sealed class SuperAdminEquipmentCreateTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Superadmin_can_explicitly_create_in_a_selected_company_without_changing_profile(bool assigned)
    {
        using var f = new Fixture("SuperAdmin", assigned);
        var request = Request(f.Beta.Id);
        var row = await new EquipmentService(f.Db).CreateAsync(f.Actor.Id, request);
        Assert.Equal(f.Beta.Id, row.CompanyId); Assert.Equal(f.Beta.Id, f.Db.Equipment.Single().CompanyId);
        Assert.Equal(assigned ? f.Alpha.Id : (Guid?)null, f.Actor.CompanyId);
    }
    [Theory]
    [InlineData("CompanyAdmin")] [InlineData("SiteManager")]
    public async Task Company_writer_cannot_forge_a_foreign_company(string role)
    {
        using var f = new Fixture(role, true);
        await Assert.ThrowsAsync<EquipmentManagementException>(() => new EquipmentService(f.Db).CreateAsync(f.Actor.Id, Request(f.Beta.Id)));
        Assert.Empty(f.Db.Equipment); Assert.Equal(f.Alpha.Id, f.Actor.CompanyId);
    }
    [Theory]
    [InlineData("missing")] [InlineData("inactive")] [InlineData("deleted")] [InlineData("empty")]
    public async Task Invalid_superadmin_company_never_creates_equipment(string kind)
    {
        using var f = new Fixture("SuperAdmin", false);
        Guid? company = kind == "missing" ? Guid.NewGuid() : kind == "empty" ? Guid.Empty : f.Beta.Id;
        if (kind == "inactive") f.Beta.IsActive = false;
        if (kind == "deleted") f.Beta.IsDeleted = true;
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<EquipmentManagementException>(() => new EquipmentService(f.Db).CreateAsync(f.Actor.Id, Request(company)));
        Assert.Empty(f.Db.Equipment);
    }
    [Fact]
    public async Task Missing_company_returns_actionable_selection_error()
    {
        using var f = new Fixture("SuperAdmin", false);
        var error = await Assert.ThrowsAsync<EquipmentManagementException>(() => new EquipmentService(f.Db).CreateAsync(f.Actor.Id, Request(null)));
        Assert.Contains("Select a company", error.Message); Assert.Empty(f.Db.Equipment);
    }
    [Fact]
    public async Task Codes_remain_unique_per_selected_company()
    {
        using var f = new Fixture("SuperAdmin", false); var service = new EquipmentService(f.Db);
        await service.CreateAsync(f.Actor.Id, Request(f.Alpha.Id)); await service.CreateAsync(f.Actor.Id, Request(f.Beta.Id));
        await Assert.ThrowsAsync<EquipmentManagementException>(() => service.CreateAsync(f.Actor.Id, Request(f.Beta.Id)));
        Assert.Equal(2, f.Db.Equipment.Count()); Assert.Null(f.Actor.CompanyId);
    }
    [Theory]
    [InlineData("SuperAdmin", 2, true)] [InlineData("CompanyAdmin", 0, false)]
    public async Task Options_are_minimal_and_server_scope_controls_company_selection(string role, int count, bool choose)
    {
        using var f = new Fixture(role, role != "SuperAdmin");
        var hidden = new Company { Name = "Hidden", Code = "HIDDEN", IsActive = false }; f.Db.Companies.Add(hidden); await f.Db.SaveChangesAsync();
        var options = await new EquipmentService(f.Db).GetCreateOptionsAsync(f.Actor.Id);
        var json = JsonSerializer.SerializeToElement(options);
        if (choose) Assert.Null(options.CompanyId);
        Assert.Equal(choose, json.GetProperty("CanSelectCompany").GetBoolean()); Assert.Equal(count, json.GetProperty("Companies").GetArrayLength());
        Assert.DoesNotContain("Hidden", json.ToString()); Assert.DoesNotContain("Email", json.ToString());
        if (!choose) Assert.Equal(f.Alpha.Id, json.GetProperty("CompanyId").GetGuid());
    }
    [Fact]
    public async Task Legacy_company_writer_payload_without_company_still_uses_own_company()
    {
        using var f = new Fixture("CompanyAdmin", true);
        var row = await new EquipmentService(f.Db).CreateAsync(f.Actor.Id, new() { EquipmentCode = "OLD", Name = "Existing client" });
        Assert.Equal(f.Alpha.Id, row.CompanyId);
    }
    private static CreateEquipmentRequest Request(Guid? company)
    {
        var request = new CreateEquipmentRequest { EquipmentCode = "EQ-005", Name = "Compactor", Category = "compactor", Make = "CAT" };
        request.CompanyId = company;
        return request;
    }
    internal sealed class Fixture : IDisposable
    {
        internal readonly FinanceOfficerAuthorityTests.TestDb Db = FinanceOfficerAuthorityTests.Database();
        internal readonly Company Alpha = new() { Code = "ALPHA", Name = "Alpha", IsActive = true };
        internal readonly Company Beta = new() { Code = "BETA", Name = "Beta", IsActive = true };
        internal readonly User Actor;
        internal Fixture(string role, bool assigned)
        {
            Actor = new() { CompanyId = assigned ? Alpha.Id : null, Email = "equipment@example.test", NormalizedEmail = "EQUIPMENT@EXAMPLE.TEST", IsActive = true,
                UserRoles = [new() { Role = FinanceOfficerAuthorityTests.Role(role, GranularPermission.Procurement_Inventory_View, GranularPermission.Procurement_Inventory_Adjust) }] };
            Db.Companies.AddRange(Alpha, Beta); Db.Users.Add(Actor); Db.SaveChanges(true);
        }
        public void Dispose() => Db.Dispose();
    }
}
