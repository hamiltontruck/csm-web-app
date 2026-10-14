namespace CSM.Application.Common.Exceptions;

public sealed class DocumentManagementException : Exception
{
    public DocumentManagementException(
        string message)
        : base(message)
    {
    }
}
