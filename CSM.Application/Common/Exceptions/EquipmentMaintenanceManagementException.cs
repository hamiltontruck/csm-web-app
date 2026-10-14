namespace CSM.Application.Common.Exceptions;

public sealed class EquipmentMaintenanceManagementException : Exception
{
    public EquipmentMaintenanceManagementException(
        string message)
        : base(message)
    {
    }
}
