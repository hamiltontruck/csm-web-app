namespace CSM.Application.Common.Exceptions;

public sealed class ReportingManagementException : Exception
{
    public ReportingManagementException(
        string message)
        : base(message)
    {
    }
}
