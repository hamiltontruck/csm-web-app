namespace CSM.Application.Common.Exceptions;

public sealed class EmployeeManagementException : Exception
{
    public EmployeeManagementException(string message)
        : base(message)
    {
    }
}