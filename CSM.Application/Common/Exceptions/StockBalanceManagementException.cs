namespace CSM.Application.Common.Exceptions;

public sealed class StockBalanceManagementException
    : Exception
{
    public StockBalanceManagementException(
        string message)
        : base(message)
    {
    }
}
