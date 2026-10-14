namespace CSM.Application.Common.Exceptions;

public sealed class ProjectPhaseManagementException : Exception
{
    public ProjectPhaseManagementException(string message)
        : base(message)
    {
    }
}