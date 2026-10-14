namespace CSM.Application.Common.Exceptions;

public sealed class StockMovementManagementException : Exception
{
    public StockMovementManagementException(
        string message)
        : base(message)
    {
    }
}
