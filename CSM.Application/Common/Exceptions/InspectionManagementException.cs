namespace CSM.Application.Common.Exceptions;

public sealed class InspectionManagementException : Exception
{
    public InspectionManagementException(
        string message)
        : base(message)
    {
    }
}
