namespace CSM.Application.Common.Exceptions;

public sealed class PayrollManagementException : Exception
{
    public PayrollManagementException(string message)
        : base(message)
    {
    }
}
