namespace CSM.Application.Finance.PettyCashAccounts.Dtos;

public sealed class PettyCashAccountResponse
{
    public Guid Id { get; set; }

    public Guid CompanyId { get; set; }

    public Guid? ConstructionSiteId { get; set; }

    public string SiteName { get; set; } = string.Empty;

    public string AccountCode { get; set; } = string.Empty;

    public string AccountName { get; set; } = string.Empty;

    public decimal OpeningBalance { get; set; }

    public decimal CurrentBalance { get; set; }

    public bool IsActive { get; set; }
}