namespace CSM.Application.Common.Exceptions;

public sealed class DailyMaterialUsageManagementException : Exception
{
    public DailyMaterialUsageManagementException(
        string message)
        : base(message)
    {
    }
}
