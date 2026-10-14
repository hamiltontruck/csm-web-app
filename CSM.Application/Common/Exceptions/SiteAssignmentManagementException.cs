namespace CSM.Application.Common.Exceptions;

public sealed class SiteAssignmentManagementException : Exception
{
    public SiteAssignmentManagementException(string message)
        : base(message)
    {
    }
}