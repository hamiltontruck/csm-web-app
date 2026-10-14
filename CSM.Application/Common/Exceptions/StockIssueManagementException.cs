namespace CSM.Application.Common.Exceptions;

public sealed class StockIssueManagementException : Exception
{
    public StockIssueManagementException(
        string message)
        : base(message)
    {
    }
}
