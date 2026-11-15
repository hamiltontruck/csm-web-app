using CSM.Application.Common.Dtos;
using CSM.Application.Finance.Invoices.Dtos;

namespace CSM.Application.Finance.Invoices;

public interface IInvoiceService
{
    Task<IReadOnlyCollection<InvoiceVendorOption>> GetVendorOptionsAsync(
        Guid currentUserId, Guid? invoiceId = null, CancellationToken cancellationToken = default,
        Guid? siteId = null, Guid? companyId = null);

    Task<IReadOnlyCollection<InvoiceResponse>> GetAllAsync(
        Guid currentUserId,
        Guid siteId,
        CancellationToken cancellationToken = default);

    Task<CompanySelectionOptions> GetCompanyOptionsAsync(
        Guid currentUserId, bool forCreate = false, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<InvoiceResponse>> GetCompanyInvoicesAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default, Guid? companyId = null);

    Task<InvoiceResponse> GetByIdAsync(
        Guid currentUserId,
        Guid invoiceId,
        CancellationToken cancellationToken = default);

    Task<InvoiceResponse> CreateAsync(
        Guid currentUserId,
        CreateInvoiceRequest request,
        CancellationToken cancellationToken = default);

    Task<InvoiceResponse> UpdateAsync(
        Guid currentUserId,
        Guid invoiceId,
        UpdateInvoiceRequest request,
        CancellationToken cancellationToken = default);

    Task<InvoiceResponse> ChangeStatusAsync(
        Guid currentUserId,
        Guid invoiceId,
        ChangeInvoiceStatusRequest request,
        CancellationToken cancellationToken = default);

    Task<InvoiceResponse> IssueAsync(
        Guid currentUserId,
        Guid invoiceId,
        CancellationToken cancellationToken = default);

    Task<InvoiceResponse> CancelAsync(
        Guid currentUserId,
        Guid invoiceId,
        CancellationToken cancellationToken = default);


}
