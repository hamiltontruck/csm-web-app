namespace CSM.Application.Common.Exceptions;

public sealed class ProjectManagementException : Exception
{
    public ProjectManagementException(string message)
        : base(message)
    {
    }
}