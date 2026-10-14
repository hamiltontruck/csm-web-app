namespace CSM.Application.Common.Exceptions;

public sealed class EquipmentAssignmentManagementException : Exception
{
    public EquipmentAssignmentManagementException(
        string message)
        : base(message)
    {
    }
}
