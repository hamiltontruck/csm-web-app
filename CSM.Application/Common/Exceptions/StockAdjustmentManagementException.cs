namespace CSM.Application.Common.Exceptions;

public sealed class StockAdjustmentManagementException
    : Exception
{
    public StockAdjustmentManagementException(
        string message)
        : base(message)
    {
    }
}
