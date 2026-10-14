namespace CSM.Application.Common.Exceptions;

public sealed class ShiftManagementException : Exception
{
    public ShiftManagementException(string message)
        : base(message)
    {
    }
}