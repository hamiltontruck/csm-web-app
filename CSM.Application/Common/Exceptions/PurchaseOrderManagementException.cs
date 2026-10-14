namespace CSM.Application.Common.Exceptions;

public sealed class PurchaseOrderManagementException : Exception
{
    public PurchaseOrderManagementException(
        string message)
        : base(message)
    {
    }
}
