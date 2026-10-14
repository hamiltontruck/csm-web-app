namespace CSM.Application.Common.Exceptions;

public sealed class ContractorManagementException : Exception
{
    public ContractorManagementException(
        string message)
        : base(message)
    {
    }
}
