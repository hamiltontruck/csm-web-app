namespace CSM.Application.Common.Exceptions;

public sealed class WorkTaskManagementException : Exception
{
    public WorkTaskManagementException(string message)
        : base(message)
    {
    }
}