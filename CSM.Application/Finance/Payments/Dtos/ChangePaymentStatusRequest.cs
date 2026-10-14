using CSM.Domain.Enums;

namespace CSM.Application.Finance.Payments.Dtos;

public sealed record ChangePaymentStatusRequest(
    PaymentStatus Status);
