using CSM.Web.Models.Finance;

namespace CSM.Web.Services;

public interface IFinanceService
{
    Task<IReadOnlyList<CostCodeResponse>> GetCostCodesAsync();

    Task<IReadOnlyList<CostCodeResponse>> GetActiveCostCodesAsync();

    Task<CostCodeResponse?> GetCostCodeAsync(Guid id);

    Task CreateCostCodeAsync(CreateCostCodeResponse model);

    Task UpdateCostCodeAsync(Guid id, UpdateCostCodeResponse model);

    Task ChangeCostCodeStatusAsync(Guid id, bool isActive);

    Task<IReadOnlyList<CostCodeResponse>> GetSiteCostCodesAsync(Guid siteId);


    Task DeleteCostCodeAsync(Guid id);

    Task<List<InvoiceResponse>> GetInvoicesAsync(Guid siteId);

    Task<List<InvoiceResponse>> GetCompanyInvoicesAsync();

    Task<InvoiceResponse?> GetInvoiceAsync(Guid id);

    Task<Guid> CreateInvoiceAsync(CreateInvoiceRequest request);

    Task IssueInvoiceAsync(Guid invoiceId);

    Task CancelInvoiceAsync(Guid invoiceId);

    Task<byte[]> DownloadInvoicePdfAsync(Guid invoiceId);

    Task<List<InvoiceLineResponse>> GetInvoiceLinesAsync(Guid invoiceId);

    Task<Guid> CreateInvoiceLineAsync(
        Guid invoiceId,
        CreateInvoiceLineRequest request);

    Task DeleteInvoiceLineAsync(Guid lineId);

    Task<InvoiceLineResponse> GetInvoiceLineAsync(Guid lineId);

    Task UpdateInvoiceLineAsync(
        Guid lineId,
        UpdateInvoiceLineRequest request);

    Task UpdateInvoiceAsync(Guid id, UpdateInvoiceRequest request);

    Task<List<PaymentResponse>> GetPaymentsAsync();
    Task<PaymentResponse> GetPaymentAsync(Guid id);

    Task<Guid> CreatePaymentAsync(CreatePaymentRequest request);

    Task UpdatePaymentAsync(
        Guid id,
        UpdatePaymentRequest request);

    Task ChangePaymentStatusAsync(
        Guid id,
        int status);

    Task<List<PaymentResponse>> GetPaymentsBySiteAsync(Guid siteId);

    Task<List<PaymentResponse>> GetPaymentsByInvoiceAsync(Guid invoiceId);

    Task CompletePaymentAsync(Guid paymentId);

    Task ReversePaymentAsync(Guid paymentId);

    Task DeletePaymentAsync(Guid paymentId);

    Task<List<PaymentResponse>> GetCompanyPaymentsAsync();

    Task<byte[]> DownloadPaymentReceiptPdfAsync(Guid paymentId);


    Task<List<BudgetResponse>> GetSiteBudgetsAsync(Guid siteId);

    Task CreateBudgetAsync(CreateBudgetModel model);

    Task<List<BudgetResponse>> GetBudgetsAsync();

    Task<BudgetResponse> GetBudgetAsync(Guid id);

    Task UpdateBudgetAsync(Guid id, UpdateBudgetModel model);

    Task ChangeBudgetStatusAsync(
        Guid budgetId,
        int status);

    Task<List<BudgetLineResponse>> GetBudgetLinesAsync(Guid budgetId);

    Task<BudgetLineResponse> GetBudgetLineAsync(Guid budgetLineId);

    Task<Guid> CreateBudgetLineAsync(CreateBudgetLineRequest request);

    Task UpdateBudgetLineAsync(
        Guid budgetLineId,
        UpdateBudgetLineRequest request);

    Task DeleteBudgetLineAsync(Guid budgetLineId);

    Task<List<ExpenseResponse>> GetExpensesAsync();

    Task<List<ExpenseResponse>> GetSiteExpensesAsync(Guid siteId);

    Task<ExpenseResponse> GetExpenseAsync(Guid id);

    Task<Guid> CreateExpenseAsync(CreateExpenseRequest request);

    Task UpdateExpenseAsync(
        Guid id,
        UpdateExpenseRequest request);

    Task ChangeExpenseStatusAsync(
        Guid id,
        int status);

        Task<List<PettyCashAccountResponse>> GetPettyCashAccountsAsync();

        Task<List<PettyCashAccountResponse>> GetSitePettyCashAccountsAsync(
            Guid siteId);

        Task<PettyCashAccountResponse> GetPettyCashAccountAsync(Guid id);

        Task<Guid> CreatePettyCashAccountAsync(
            CreatePettyCashAccountRequest request);

        Task UpdatePettyCashAccountAsync(
            Guid id,
            UpdatePettyCashAccountRequest request);

        Task<List<PettyCashTransactionResponse>> GetPettyCashTransactionsAsync(
            Guid pettyCashAccountId);

        Task<Guid> CreatePettyCashTransactionAsync(
            CreatePettyCashTransactionRequest request);

        Task<CompanyFinancialSummaryResponse> GetCompanyFinancialSummaryAsync();

        Task<SiteFinancialSummaryResponse> GetSiteFinancialSummaryAsync(
            Guid constructionSiteId);



}