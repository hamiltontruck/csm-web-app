namespace CSM.Application.Common.Exceptions;

public sealed class StockReturnManagementException : Exception
{
    public StockReturnManagementException(
        string message)
        : base(message)
    {
    }
}
