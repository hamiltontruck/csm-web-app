namespace CSM.Application.Common.Exceptions;

public sealed class PaymentManagementException : Exception
{
    public PaymentManagementException(
        string message)
        : base(message)
    {
    }
}
