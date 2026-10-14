namespace CSM.Application.Common.Exceptions;

public sealed class PettyCashManagementException : Exception
{
    public PettyCashManagementException(string message)
        : base(message)
    {
    }
}