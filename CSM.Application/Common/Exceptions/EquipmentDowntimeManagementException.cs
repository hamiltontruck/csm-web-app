namespace CSM.Application.Common.Exceptions;

public sealed class EquipmentDowntimeManagementException
    : Exception
{
    public EquipmentDowntimeManagementException(
        string message)
        : base(message)
    {
    }
}
