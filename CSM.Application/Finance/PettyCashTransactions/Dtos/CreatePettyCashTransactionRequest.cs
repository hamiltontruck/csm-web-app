namespace CSM.Application.Finance.PettyCashTransactions.Dtos;

public sealed class CreatePettyCashTransactionRequest
{
    public Guid PettyCashAccountId { get; set; }

    public string TransactionType { get; set; } = string.Empty;

    public string ReferenceNumber { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Description { get; set; } = string.Empty;

    public string? Remarks { get; set; }

    public Guid? CostCodeId { get; set; }
}