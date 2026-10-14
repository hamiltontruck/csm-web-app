using System.ComponentModel.DataAnnotations;

namespace CSM.Application.Finance.PettyCashAccounts.Dtos;

public sealed class CreatePettyCashAccountRequest
{
    public Guid? ConstructionSiteId { get; set; }

    [Required]
    [MaxLength(30)]
    public string AccountCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string AccountName { get; set; } = string.Empty;

    [Range(0, 999999999)]
    public decimal OpeningBalance { get; set; }
}