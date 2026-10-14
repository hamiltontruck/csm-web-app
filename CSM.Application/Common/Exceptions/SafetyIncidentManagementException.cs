namespace CSM.Application.Common.Exceptions;

public sealed class SafetyIncidentManagementException : Exception
{
    public SafetyIncidentManagementException(
        string message)
        : base(message)
    {
    }
}
