namespace CSM.Application.Common.Exceptions;

public sealed class GoodsReceiptManagementException : Exception
{
    public GoodsReceiptManagementException(
        string message)
        : base(message)
    {
    }
}
