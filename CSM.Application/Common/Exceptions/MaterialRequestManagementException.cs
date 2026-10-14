namespace CSM.Application.Common.Exceptions;

public sealed class MaterialRequestManagementException : Exception
{
    public MaterialRequestManagementException(
        string message)
        : base(message)
    {
    }
}
