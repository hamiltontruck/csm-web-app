using CSM.Web.Models.Projects;
using CSM.Web.Models.Procurement;
namespace CSM.Web.Models.Finance;

public sealed class FinanceSiteOptions
{
    public Guid SiteId { get; set; }
    public Guid CompanyId { get; set; }
    public string SiteCode { get; set; } = "";
    public string SiteName { get; set; } = "";
    public List<ProjectResponse> Projects { get; set; } = [];
    public List<CostCodeResponse> CostCodes { get; set; } = [];
    public List<VendorResponse> Vendors { get; set; } = [];
}
