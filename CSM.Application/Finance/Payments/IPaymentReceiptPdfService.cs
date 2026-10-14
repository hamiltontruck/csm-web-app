using CSM.Application.Finance.Payments.Dtos;

namespace CSM.Application.Finance.Payments;

public interface IPaymentReceiptPdfService
{
    byte[] Generate(PaymentResponse payment);

    Task<byte[]> GenerateAsync(
        Guid currentUserId,
        Guid paymentId,
        CancellationToken cancellationToken = default);
}