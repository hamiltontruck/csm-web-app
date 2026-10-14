namespace CSM.Web.Models.Finance;

public sealed record CreatePettyCashTransactionRequest(
    Guid PettyCashAccountId,
    string TransactionType,
    string ReferenceNumber,
    decimal Amount,
    string Description,
    string? Remarks,
    Guid? CostCodeId);