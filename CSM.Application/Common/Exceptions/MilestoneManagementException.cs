namespace CSM.Application.Common.Exceptions;

public sealed class MilestoneManagementException : Exception
{
    public MilestoneManagementException(string message)
        : base(message)
    {
    }
}