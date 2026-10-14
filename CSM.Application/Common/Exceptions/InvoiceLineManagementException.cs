namespace CSM.Application.Common.Exceptions;

public sealed class InvoiceLineManagementException : Exception
{
    public InvoiceLineManagementException(
        string message)
        : base(message)
    {
    }
}
