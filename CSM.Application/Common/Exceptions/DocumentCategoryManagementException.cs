namespace CSM.Application.Common.Exceptions;

public sealed class DocumentCategoryManagementException : Exception
{
    public DocumentCategoryManagementException(
        string message)
        : base(message)
    {
    }
}
