namespace CSM.Application.Common.Exceptions;

public sealed class UserManagementException : Exception
{
    public UserManagementException(string message)
        : base(message)
    {
    }
}