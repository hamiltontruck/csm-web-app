namespace CSM.Application.Common.Exceptions;

public sealed class MaterialManagementException : Exception
{
    public MaterialManagementException(
        string message)
        : base(message)
    {
    }
}
