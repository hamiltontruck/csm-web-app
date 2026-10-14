namespace CSM.Application.Common.Exceptions;

public sealed class InvoiceManagementException : Exception
{
    public InvoiceManagementException(
        string message)
        : base(message)
    {
    }
}
