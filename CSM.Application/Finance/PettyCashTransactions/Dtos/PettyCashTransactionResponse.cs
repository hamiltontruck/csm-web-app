namespace CSM.Application.Finance.PettyCashTransactions.Dtos;

public sealed class PettyCashTransactionResponse
{
    public Guid Id { get; set; }

    public Guid PettyCashAccountId { get; set; }

    public Guid? CostCodeId { get; set; }

    public string? CostCodeName { get; set; }

    public string ReferenceNumber { get; set; } = string.Empty;

    public string TransactionType { get; set; } = string.Empty;

    public DateTime TransactionDateUtc { get; set; }

    public decimal Amount { get; set; }

    public decimal BalanceBefore { get; set; }

    public decimal BalanceAfter { get; set; }

    public string Description { get; set; } = string.Empty;

    public string? Remarks { get; set; }

    public bool IsApproved { get; set; }
}