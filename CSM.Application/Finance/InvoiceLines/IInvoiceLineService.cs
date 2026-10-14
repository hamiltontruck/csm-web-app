using CSM.Application.Finance.InvoiceLines.Dtos;

namespace CSM.Application.Finance.InvoiceLines;

public interface IInvoiceLineService
{
    Task<IReadOnlyCollection<InvoiceLineResponse>> GetByInvoiceAsync(
        Guid currentUserId,
        Guid invoiceId,
        CancellationToken cancellationToken = default);

    Task<InvoiceLineResponse> GetByIdAsync(
        Guid currentUserId,
        Guid invoiceLineId,
        CancellationToken cancellationToken = default);

    Task<InvoiceLineResponse> CreateAsync(
        Guid currentUserId,
        Guid invoiceId,
        CreateInvoiceLineRequest request,
        CancellationToken cancellationToken = default);

    Task<InvoiceLineResponse> UpdateAsync(
        Guid currentUserId,
        Guid invoiceLineId,
        UpdateInvoiceLineRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid currentUserId,
        Guid invoiceLineId,
        CancellationToken cancellationToken = default);
}
