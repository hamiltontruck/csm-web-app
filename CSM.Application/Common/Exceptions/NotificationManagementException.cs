namespace CSM.Application.Common.Exceptions;

public sealed class NotificationManagementException : Exception
{
    public NotificationManagementException(
        string message)
        : base(message)
    {
    }
}
