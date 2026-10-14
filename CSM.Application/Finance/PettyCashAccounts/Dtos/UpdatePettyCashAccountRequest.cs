using System.ComponentModel.DataAnnotations;

namespace CSM.Application.Finance.PettyCashAccounts.Dtos;

public sealed class UpdatePettyCashAccountRequest
{
    [Required]
    [MaxLength(100)]
    public string AccountName { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}