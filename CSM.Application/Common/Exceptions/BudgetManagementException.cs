namespace CSM.Application.Common.Exceptions;

public sealed class BudgetManagementException : Exception
{
    public BudgetManagementException(
        string message)
        : base(message)
    {
    }
}
