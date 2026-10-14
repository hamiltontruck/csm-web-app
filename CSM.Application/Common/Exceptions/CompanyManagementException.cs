namespace CSM.Application.Common.Exceptions;

public sealed class CompanyManagementException : Exception
{
    public CompanyManagementException(string message)
        : base(message)
    {
    }
}