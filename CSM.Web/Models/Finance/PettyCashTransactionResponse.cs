namespace CSM.Web.Models.Finance;

public sealed record PettyCashTransactionResponse(
    Guid Id,
    Guid PettyCashAccountId,
    Guid? CostCodeId,
    string? CostCodeName,
    string ReferenceNumber,
    string TransactionType,
    DateTime TransactionDateUtc,
    decimal Amount,
    decimal BalanceBefore,
    decimal BalanceAfter,
    string Description,
    string? Remarks,
    bool IsApproved);