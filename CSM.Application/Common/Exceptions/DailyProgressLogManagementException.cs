namespace CSM.Application.Common.Exceptions;

public sealed class DailyProgressLogManagementException : Exception
{
    public DailyProgressLogManagementException(string message)
        : base(message)
    {
    }
}