using CSM.Application.Finance.Invoices.Dtos;

namespace CSM.Application.Finance.Invoices;

public interface IInvoicePdfService
{
    byte[] Generate(InvoiceResponse invoice);

    Task<byte[]> GenerateAsync(
        Guid currentUserId,
        Guid invoiceId,
        CancellationToken cancellationToken = default);
}