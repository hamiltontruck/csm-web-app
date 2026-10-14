namespace CSM.Application.Common.Exceptions;

public sealed class BudgetLineManagementException : Exception
{
    public BudgetLineManagementException(
        string message)
        : base(message)
    {
    }
}
