namespace CSM.Application.Common.Exceptions;

public sealed class ExpenseManagementException : Exception
{
    public ExpenseManagementException(
        string message)
        : base(message)
    {
    }
}
