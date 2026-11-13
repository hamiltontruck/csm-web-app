namespace CSM.Web.Models.Finance;
public sealed record ExpenseSettlementEvidence(string Source, Guid Id, string Number, string Status, decimal Amount, string CurrencyCode);
public sealed record ExpenseSettlementSummaryResponse(decimal TotalDue, decimal SettledAmount, decimal? OutstandingAmount,
    bool IsVerified, bool IsLegacyPaid, string State, IReadOnlyList<ExpenseSettlementEvidence> Evidence);
public sealed record ExpenseSettlementOption(Guid Id, string ExpenseNumber, Guid CompanyId, Guid? ConstructionSiteId,
    Guid? ProjectId, Guid? VendorId, string? VendorName, Guid CostCodeId, string CurrencyCode,
    decimal ExchangeRate, decimal TotalDue, decimal OutstandingAmount);
public sealed record ExpenseCashAccountOption(Guid Id, string AccountCode, string AccountName, decimal CurrentBalance);
public sealed record ExpenseSettlementOptionsResponse(ExpenseSettlementOption Expense, IReadOnlyList<ExpenseCashAccountOption> CashAccounts);
