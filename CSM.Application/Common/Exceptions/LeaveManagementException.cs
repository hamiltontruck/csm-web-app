namespace CSM.Application.Common.Exceptions;

public sealed class LeaveManagementException : Exception
{
    public LeaveManagementException(string message)
        : base(message)
    {
    }
}
