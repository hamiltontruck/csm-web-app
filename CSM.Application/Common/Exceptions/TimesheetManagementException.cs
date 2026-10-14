namespace CSM.Application.Common.Exceptions;

public sealed class TimesheetManagementException : Exception
{
    public TimesheetManagementException(string message)
        : base(message)
    {
    }
}
