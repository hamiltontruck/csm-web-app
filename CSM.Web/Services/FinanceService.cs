using System.Net.Http.Headers;
using System.Net.Http.Json;

using CSM.Web.Models.Finance;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace CSM.Web.Services;

public class FinanceService : IFinanceService
{
    private readonly IHttpClientFactory _factory;
    private readonly ProtectedSessionStorage _storage;

    public FinanceService(
        IHttpClientFactory factory,
        ProtectedSessionStorage storage)
    {
        _factory = factory;
        _storage = storage;
    }

    private async Task<HttpClient> ClientAsync()
    {
        var client = _factory.CreateClient("CSM.Api");

        var token = await _storage.GetAsync<string>("jwt");

        if (token.Success && !string.IsNullOrWhiteSpace(token.Value))
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token.Value);
        }

        return client;
    }

    public async Task<IReadOnlyList<CostCodeResponse>> GetCostCodesAsync()
    {
        var client = await ClientAsync();

        return await client.GetFromJsonAsync<List<CostCodeResponse>>("CostCodes")
            ?? [];
    }

    public async Task<IReadOnlyList<CostCodeResponse>> GetActiveCostCodesAsync()
    {
        var client = await ClientAsync();

        return await client.GetFromJsonAsync<List<CostCodeResponse>>("CostCodes/active")
            ?? [];
    }

    public async Task<CostCodeResponse?> GetCostCodeAsync(Guid id)
    {
        var client = await ClientAsync();

        return await client.GetFromJsonAsync<CostCodeResponse>($"CostCodes/{id}");
    }

    public async Task CreateCostCodeAsync(CreateCostCodeResponse model)
    {
        var client = await ClientAsync();

        var response = await client.PostAsJsonAsync("CostCodes", model);

        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateCostCodeAsync(Guid id, UpdateCostCodeResponse model)
    {
        var client = await ClientAsync();

        var response = await client.PutAsJsonAsync(
            $"CostCodes/{id}",
            model);

        response.EnsureSuccessStatusCode();
    }

    public async Task ChangeCostCodeStatusAsync(Guid id, bool isActive)
    {
        var client = await ClientAsync();

        var response = await client.PutAsJsonAsync(
            $"CostCodes/{id}/active-status",
            new { IsActive = isActive });

        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<CostCodeResponse>> GetSiteCostCodesAsync(Guid siteId)
    {
        var client = await ClientAsync();

        return await client.GetFromJsonAsync<List<CostCodeResponse>>(
            $"ConstructionSites/{siteId}/cost-codes") ?? [];
    }

    public async Task DeleteCostCodeAsync(Guid id)
    {
        var client = await ClientAsync();

        var response = await client.DeleteAsync($"CostCodes/{id}");

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception(error);
        }
    }

    public async Task<List<InvoiceResponse>> GetInvoicesAsync(Guid siteId)
    {
        var client = await ClientAsync();

        return await client.GetFromJsonAsync<List<InvoiceResponse>>(
            $"Invoices?siteId={siteId}") ?? new();
    }

    public async Task<List<InvoiceResponse>> GetCompanyInvoicesAsync()
    {
        var client = await ClientAsync();

        var result = await client.GetFromJsonAsync<List<InvoiceResponse>>(
            "Invoices/company") ?? new();

        Console.WriteLine($"Company invoices: {result.Count}");

        return result;
    }

    public async Task<InvoiceResponse?> GetInvoiceAsync(Guid id)
    {
        var client = await ClientAsync();

        return await client.GetFromJsonAsync<InvoiceResponse>(
            $"Invoices/{id}");
    }

    public async Task<Guid> CreateInvoiceAsync(CreateInvoiceRequest request)
    {
        var client = await ClientAsync();

        var response = await client.PostAsJsonAsync("Invoices", request);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception(error);
        }

        var invoice = await response.Content.ReadFromJsonAsync<InvoiceResponse>()
                      ?? throw new Exception("Failed to read invoice response.");

        return invoice.Id;
    }

    public async Task IssueInvoiceAsync(Guid invoiceId)
    {
        var client = await ClientAsync();

        var response = await client.PutAsync(
            $"Invoices/{invoiceId}/issue",
            null);

        response.EnsureSuccessStatusCode();
    }

    public async Task CancelInvoiceAsync(Guid invoiceId)
    {
        var client = await ClientAsync();

        var response = await client.PutAsync(
            $"Invoices/{invoiceId}/cancel",
            null);

        response.EnsureSuccessStatusCode();
    }

    public async Task<byte[]> DownloadInvoicePdfAsync(Guid invoiceId)
    {
        var client = await ClientAsync();

        return await client.GetByteArrayAsync(
            $"Invoices/{invoiceId}/print");
    }

    public async Task<byte[]> DownloadPaymentReceiptPdfAsync(Guid paymentId)
    {
        var client = await ClientAsync();

        return await client.GetByteArrayAsync(
            $"Payments/{paymentId}/print");
    }

    public async Task<List<InvoiceLineResponse>> GetInvoiceLinesAsync(Guid invoiceId)
    {
        var client = await ClientAsync();

        return await client.GetFromJsonAsync<List<InvoiceLineResponse>>(
            $"InvoiceLines/invoice/{invoiceId}") ?? new();
    }

    public async Task<Guid> CreateInvoiceLineAsync(
        Guid invoiceId,
        CreateInvoiceLineRequest request)
    {
        var client = await ClientAsync();

        var response = await client.PostAsJsonAsync(
            $"InvoiceLines/invoice/{invoiceId}",
            request);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception(error);
        }

        var line = await response.Content
            .ReadFromJsonAsync<InvoiceLineResponse>();

        return line!.Id;
    }

    public async Task DeleteInvoiceLineAsync(Guid lineId)
    {
        var client = await ClientAsync();

        var response = await client.DeleteAsync(
            $"InvoiceLines/{lineId}");

        response.EnsureSuccessStatusCode();
    }

    public async Task<InvoiceLineResponse> GetInvoiceLineAsync(Guid lineId)
    {
        var client = await ClientAsync();

        return await client.GetFromJsonAsync<InvoiceLineResponse>(
            $"InvoiceLines/{lineId}")
            ?? throw new Exception("Invoice line not found.");
    }

    public async Task UpdateInvoiceLineAsync(
        Guid lineId,
        UpdateInvoiceLineRequest request)
    {
        var client = await ClientAsync();

        var response = await client.PutAsJsonAsync(
            $"InvoiceLines/{lineId}",
            request);

        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateInvoiceAsync(Guid id, UpdateInvoiceRequest request)
    {
        var client = await ClientAsync();

        var response = await client.PutAsJsonAsync($"Invoices/{id}", request);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception(error);
        }
    }
    public async Task<List<PaymentResponse>> GetPaymentsAsync()
    {
        var client = await ClientAsync();

        return await client.GetFromJsonAsync<List<PaymentResponse>>(
            "Payments") ?? [];
    }

    public async Task<PaymentResponse> GetPaymentAsync(Guid id)
    {
        var client = await ClientAsync();

        return await client.GetFromJsonAsync<PaymentResponse>(
            $"Payments/{id}")
            ?? throw new Exception("Payment not found.");
    }

    public async Task<Guid> CreatePaymentAsync(CreatePaymentRequest request)
    {
        var client = await ClientAsync();

        var response = await client.PostAsJsonAsync("Payments", request);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception(error);
        }

        var payment = await response.Content.ReadFromJsonAsync<PaymentResponse>();

        return payment!.Id;
    }

    public async Task UpdatePaymentAsync(
        Guid id,
        UpdatePaymentRequest request)
    {
        var client = await ClientAsync();

        var response = await client.PutAsJsonAsync(
            $"Payments/{id}",
            request);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception(error);
        }
    }

    public async Task ChangePaymentStatusAsync(
        Guid id,
        int status)
    {
        var client = await ClientAsync();

        var response = await client.PutAsJsonAsync(
            $"Payments/{id}/status",
            new { status });

        response.EnsureSuccessStatusCode();
    }

    public async Task<List<PaymentResponse>> GetPaymentsBySiteAsync(Guid siteId)
    {
        var client = await ClientAsync();

        var payments = await client.GetFromJsonAsync<List<PaymentResponse>>("Payments")
                       ?? [];

        return payments
            .Where(x => x.ConstructionSiteId == siteId)
            .OrderByDescending(x => x.PaymentDate)
            .ToList();
    }

    public async Task<List<PaymentResponse>> GetPaymentsByInvoiceAsync(Guid invoiceId)
    {
        var client = await ClientAsync();

        var payments = await client.GetFromJsonAsync<List<PaymentResponse>>("Payments")
                       ?? [];

        return payments
            .Where(x => x.InvoiceId == invoiceId)
            .OrderByDescending(x => x.PaymentDate)
            .ToList();
    }

    public async Task CompletePaymentAsync(Guid paymentId)
    {
        var client = await ClientAsync();

        var response = await client.PostAsync(
            $"Payments/{paymentId}/complete",
            null);

        response.EnsureSuccessStatusCode();
    }

    public async Task ReversePaymentAsync(Guid paymentId)
    {
        var client = await ClientAsync();

        var response = await client.PostAsync(
            $"Payments/{paymentId}/reverse",
            null);

        response.EnsureSuccessStatusCode();
    }

    public async Task<List<PaymentResponse>> GetCompanyPaymentsAsync()
    {
        var client = await ClientAsync();

        return await client.GetFromJsonAsync<List<PaymentResponse>>(
            "Payments/company") ?? new();
    }

    public async Task DeletePaymentAsync(Guid paymentId)
    {
        var client = await ClientAsync();

        var response = await client.DeleteAsync(
            $"Payments/{paymentId}");

        response.EnsureSuccessStatusCode();
    }

    public async Task<List<BudgetResponse>> GetSiteBudgetsAsync(Guid siteId)
    {
        var client = await ClientAsync();

        return await client.GetFromJsonAsync<List<BudgetResponse>>(
            $"ConstructionSites/{siteId}/budgets") ?? new();
    }

    public async Task CreateBudgetAsync(CreateBudgetModel model)
    {
        var client = await ClientAsync();

        var response = await client.PostAsJsonAsync("Budgets", model);

        response.EnsureSuccessStatusCode();
    }

    public async Task<List<BudgetResponse>> GetBudgetsAsync()
    {
        var client = await ClientAsync();

        return await client.GetFromJsonAsync<List<BudgetResponse>>("Budgets")
            ?? new();
    }

    public async Task<BudgetResponse> GetBudgetAsync(Guid id)
    {
        var client = await ClientAsync();

        return await client.GetFromJsonAsync<BudgetResponse>($"Budgets/{id}")
            ?? throw new Exception("Budget not found.");
    }

    public async Task UpdateBudgetAsync(Guid id, UpdateBudgetModel model)
    {
        var client = await ClientAsync();

        var response = await client.PutAsJsonAsync($"Budgets/{id}", model);

        response.EnsureSuccessStatusCode();
    }

    public async Task ChangeBudgetStatusAsync(
        Guid budgetId,
        int status)
    {
        var client = await ClientAsync();

        var response = await client.PutAsJsonAsync(
            $"Budgets/{budgetId}/status",
            new { status });

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception(error);
        }
    }

    public async Task<List<BudgetLineResponse>> GetBudgetLinesAsync(
        Guid budgetId)
    {
        var client = await ClientAsync();

        return await client.GetFromJsonAsync<List<BudgetLineResponse>>(
            $"BudgetLines/budget/{budgetId}") ?? new();
    }

    public async Task<BudgetLineResponse> GetBudgetLineAsync(
        Guid budgetLineId)
    {
        var client = await ClientAsync();

        return await client.GetFromJsonAsync<BudgetLineResponse>(
            $"BudgetLines/{budgetLineId}")
            ?? throw new Exception("Budget line not found.");
    }

    public async Task<Guid> CreateBudgetLineAsync(
        CreateBudgetLineRequest request)
    {
        var client = await ClientAsync();

        using var cts =
            new CancellationTokenSource(
                TimeSpan.FromSeconds(10));

        try
        {
            var response = await client.PostAsJsonAsync(
                "BudgetLines",
                request,
                cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                var error =
                    await response.Content.ReadAsStringAsync();

                throw new Exception(
                    $"Budget line creation failed. " +
                    $"HTTP {(int)response.StatusCode} " +
                    $"{response.StatusCode}: {error}");
            }

            var line = await response.Content
                .ReadFromJsonAsync<BudgetLineResponse>(
                    cancellationToken: cts.Token)
                ?? throw new Exception(
                    "Failed to read budget line response.");

            return line.Id;
        }
        catch (OperationCanceledException)
        {
            throw new Exception(
                "Budget line creation timed out after 10 seconds. " +
                "The Web application did not receive a response from the API.");
        }
    }

    public async Task UpdateBudgetLineAsync(
        Guid budgetLineId,
        UpdateBudgetLineRequest request)
    {
        var client = await ClientAsync();

        var response = await client.PutAsJsonAsync(
            $"BudgetLines/{budgetLineId}",
            request);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception(error);
        }
    }

    public async Task DeleteBudgetLineAsync(
        Guid budgetLineId)
    {
        var client = await ClientAsync();

        var response = await client.DeleteAsync(
            $"BudgetLines/{budgetLineId}");

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();

            throw new Exception(error);
        }
    }

    public async Task<List<ExpenseResponse>> GetExpensesAsync()
    {
        var client = await ClientAsync();

        return await client.GetFromJsonAsync<List<ExpenseResponse>>(
            "Expenses") ?? new();
    }

    public async Task<List<ExpenseResponse>> GetSiteExpensesAsync(Guid siteId)
    {
        var client = await ClientAsync();

        return await client.GetFromJsonAsync<List<ExpenseResponse>>(
            $"Expenses/site/{siteId}") ?? new();
    }

    public async Task<ExpenseResponse> GetExpenseAsync(Guid id)
    {
        var client = await ClientAsync();

        return await client.GetFromJsonAsync<ExpenseResponse>(
            $"Expenses/{id}")
            ?? throw new Exception("Expense not found.");
    }

    public async Task<Guid> CreateExpenseAsync(
        CreateExpenseRequest request)
    {
        var client = await ClientAsync();

        var response = await client.PostAsJsonAsync(
            "Expenses",
            request);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();

            throw new Exception(error);
        }

        var expense = await response.Content
            .ReadFromJsonAsync<ExpenseResponse>()
            ?? throw new Exception(
                "Failed to read expense response.");

        return expense.Id;
    }

    public async Task UpdateExpenseAsync(
        Guid id,
        UpdateExpenseRequest request)
    {
        var client = await ClientAsync();

        var response = await client.PutAsJsonAsync(
            $"Expenses/{id}",
            request);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();

            throw new Exception(error);
        }
    }

    public async Task ChangeExpenseStatusAsync(
        Guid id,
        int status)
    {
        var client = await ClientAsync();

        var response = await client.PutAsJsonAsync(
            $"Expenses/{id}/status",
            new
            {
                Status = status
            });

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();

            throw new Exception(error);
        }
    }

    public async Task<List<PettyCashAccountResponse>> GetPettyCashAccountsAsync()
        {
            var client = await ClientAsync();

            return await client.GetFromJsonAsync<List<PettyCashAccountResponse>>(
                "PettyCashAccounts") ?? new();
        }

        public async Task<List<PettyCashAccountResponse>> GetSitePettyCashAccountsAsync(
            Guid siteId)
        {
            var client = await ClientAsync();

            return await client.GetFromJsonAsync<List<PettyCashAccountResponse>>(
                $"PettyCashAccounts/site/{siteId}") ?? new();
        }

    public async Task<PettyCashAccountResponse> GetPettyCashAccountAsync(Guid id)
            {
                var client = await ClientAsync();

                return await client.GetFromJsonAsync<PettyCashAccountResponse>(
                    $"PettyCashAccounts/{id}")
                    ?? throw new Exception("Petty cash account not found.");
            }

        public async Task<Guid> CreatePettyCashAccountAsync(
            CreatePettyCashAccountRequest request)
        {
            var client = await ClientAsync();

            var response = await client.PostAsJsonAsync(
                "PettyCashAccounts",
                request);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();

                throw new Exception(error);
            }

            var account = await response.Content
                .ReadFromJsonAsync<PettyCashAccountResponse>()
                ?? throw new Exception(
                    "Failed to read petty cash account response.");

            return account.Id;
        }

            public async Task UpdatePettyCashAccountAsync(
                Guid id,
                UpdatePettyCashAccountRequest request)
            {
                var client = await ClientAsync();

                var response = await client.PutAsJsonAsync(
                    $"PettyCashAccounts/{id}",
                    request);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();

                    throw new Exception(error);
                }
            }

        public async Task<List<PettyCashTransactionResponse>> GetPettyCashTransactionsAsync(
            Guid pettyCashAccountId)
        {
            var client = await ClientAsync();

            return await client.GetFromJsonAsync<List<PettyCashTransactionResponse>>(
                $"PettyCashTransactions?pettyCashAccountId={pettyCashAccountId}")
                ?? new();
        }

        public async Task<Guid> CreatePettyCashTransactionAsync(
            CreatePettyCashTransactionRequest request)
        {
            var client = await ClientAsync();

            var response = await client.PostAsJsonAsync(
                "PettyCashTransactions",
                request);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception(error);
            }

            var transaction = await response.Content
                .ReadFromJsonAsync<PettyCashTransactionResponse>()
                ?? throw new Exception(
                    "Failed to read petty cash transaction response.");

            return transaction.Id;
        }

        public async Task<CompanyFinancialSummaryResponse>
            GetCompanyFinancialSummaryAsync()
        {
            var client = await ClientAsync();

            return await client.GetFromJsonAsync<CompanyFinancialSummaryResponse>(
                "FinancialSummary/company")
                ?? throw new Exception(
                    "Failed to read company financial summary.");
        }

        public async Task<SiteFinancialSummaryResponse>
            GetSiteFinancialSummaryAsync(Guid constructionSiteId)
        {
            var client = await ClientAsync();

            return await client.GetFromJsonAsync<SiteFinancialSummaryResponse>(
                $"FinancialSummary/site/{constructionSiteId}")
                ?? throw new Exception(
                    "Failed to read site financial summary.");
        }
}
