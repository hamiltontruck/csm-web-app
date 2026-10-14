namespace CSM.Application.Common.Exceptions;

public sealed class StockTransferManagementException : Exception
{
    public StockTransferManagementException(
        string message)
        : base(message)
    {
    }
}
