namespace CSM.Application.Common.Exceptions;

public sealed class VendorManagementException : Exception
{
    public VendorManagementException(
        string message)
        : base(message)
    {
    }
}
