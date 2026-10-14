namespace CSM.Application.Common.Exceptions;

public sealed class AttendanceManagementException : Exception
{
    public AttendanceManagementException(string message)
        : base(message)
    {
    }
}