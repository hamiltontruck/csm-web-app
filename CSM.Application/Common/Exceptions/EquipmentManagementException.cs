namespace CSM.Application.Common.Exceptions;

public sealed class EquipmentManagementException : Exception
{
    public EquipmentManagementException(
        string message)
        : base(message)
    {
    }
}
