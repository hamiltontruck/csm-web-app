using System.Text.Json;
using CSM.Domain.Entities.Finance;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Entities.Projects;
using CSM.Domain.Enums;
using CSM.Infrastructure.Services;
using Xunit;
namespace CSM.Infrastructure.Tests;
public sealed class FinanceOfficerFormOptionsTests
{
    [Theory]
    [InlineData("budget", true)] [InlineData("invoice", true)] [InlineData("expense", true)] [InlineData("payment", true)]
    [InlineData("budget", false)] [InlineData("invoice", false)] [InlineData("expense", false)] [InlineData("payment", false)]
    public async Task Finance_owned_options_require_assigned_site_and_expose_minimal_matching_references(string purpose, bool assigned)
    {
        using var db = FinanceOfficerAuthorityTests.Database(); var actor = FinanceOfficerAuthorityTests.Actor(); db.Users.Add(actor); var company = actor.CompanyId!.Value;
        var site = new ConstructionSite { CompanyId = company, Name = "Assigned", SiteCode = "A", IsActive = true }; db.ConstructionSites.Add(site);
        if (assigned) db.UserSiteAccesses.Add(new() { UserId = actor.Id, CompanyId = company, SiteId = site.Id, IsActive = true });
        var project = new Project { CompanyId = company, ConstructionSiteId = site.Id, ProjectCode = "P", Name = "Own" }; db.Projects.Add(project);
        db.Projects.Add(new Project { CompanyId = company, ConstructionSiteId = Guid.NewGuid(), ProjectCode = "OTHER", Name = "Other" });
        db.CostCodes.Add(new CostCode { CompanyId = company, Code = "SHARED", Name = "Shared", IsActive = true });
        db.CostCodes.Add(new CostCode { CompanyId = company, ConstructionSiteId = Guid.NewGuid(), Code = "OTHER", Name = "Other", IsActive = true });
        db.Vendors.Add(new Vendor { CompanyId = company, VendorCode = "OWN", Name = "Own", IsActive = true, ContactPerson = "PRIVATE-CONTACT" });
        db.Vendors.Add(new Vendor { CompanyId = Guid.NewGuid(), VendorCode = "FOREIGN", Name = "Foreign", IsActive = true });
        await db.SaveChangesAsync();
        var type = typeof(InvoiceService).Assembly.GetType("CSM.Infrastructure.Services.FinanceSiteOptionsService"); Assert.NotNull(type);
        var service = Activator.CreateInstance(type, db)!;
        async Task<JsonElement> Read() {
            var task = (Task)type.GetMethod("GetAsync")!.Invoke(service, [actor.Id, site.Id, purpose, CancellationToken.None])!; await task;
            return JsonSerializer.SerializeToElement(task.GetType().GetProperty("Result")!.GetValue(task));
        }
        if (!assigned) { Assert.NotNull(await Record.ExceptionAsync(Read)); return; }
        var result = await Read();
        Assert.Equal(site.Id, result.GetProperty("SiteId").GetGuid());
        Assert.Equal(project.Id, Assert.Single(result.GetProperty("Projects").EnumerateArray()).GetProperty("Id").GetGuid());
        Assert.DoesNotContain("OTHER", result.GetRawText()); Assert.DoesNotContain("FOREIGN", result.GetRawText()); Assert.DoesNotContain("PRIVATE-CONTACT", result.GetRawText());
        if (purpose is "budget" or "expense") Assert.Equal("SHARED", Assert.Single(result.GetProperty("CostCodes").EnumerateArray()).GetProperty("Code").GetString());
        if (purpose == "budget") Assert.Empty(result.GetProperty("Vendors").EnumerateArray());
    }
}
