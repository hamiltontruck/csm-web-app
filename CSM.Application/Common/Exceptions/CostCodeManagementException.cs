namespace CSM.Application.Common.Exceptions;

public sealed class CostCodeManagementException : Exception
{
    public CostCodeManagementException(
        string message)
        : base(message)
    {
    }
}
