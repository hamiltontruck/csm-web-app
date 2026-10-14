namespace CSM.Application.Common.Exceptions;

public sealed class PermitManagementException : Exception
{
    public PermitManagementException(
        string message)
        : base(message)
    {
    }
}
