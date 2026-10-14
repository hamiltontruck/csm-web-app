using CSM.Application.Finance.Payments.Dtos;

namespace CSM.Application.Finance.Payments;

public interface IPaymentService
{
    Task<IReadOnlyCollection<PaymentResponse>> GetAllAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<PaymentResponse> GetByIdAsync(
        Guid currentUserId,
        Guid paymentId,
        CancellationToken cancellationToken = default);

    Task<PaymentResponse> CreateAsync(
        Guid currentUserId,
        CreatePaymentRequest request,
        CancellationToken cancellationToken = default);

    Task<PaymentResponse> UpdateAsync(
        Guid currentUserId,
        Guid paymentId,
        UpdatePaymentRequest request,
        CancellationToken cancellationToken = default);

    Task<PaymentResponse> ChangeStatusAsync(
        Guid currentUserId,
        Guid paymentId,
        ChangePaymentStatusRequest request,
        CancellationToken cancellationToken = default);

    Task CompletePaymentAsync(
        Guid currentUserId,
        Guid paymentId,
        CancellationToken cancellationToken = default);

    Task ReversePaymentAsync(
        Guid currentUserId,
        Guid paymentId,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid currentUserId,
        Guid paymentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<PaymentResponse>> GetCompanyPaymentsAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default);
}
