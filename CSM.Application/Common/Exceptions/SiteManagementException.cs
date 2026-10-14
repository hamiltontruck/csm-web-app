namespace CSM.Application.Common.Exceptions;

public sealed class SiteManagementException : Exception
{
    public SiteManagementException(string message)
        : base(message)
    {
    }
}