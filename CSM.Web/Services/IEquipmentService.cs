using CSM.Web.Models.Equipment;

namespace CSM.Web.Services;

public interface IEquipmentService
{
    Task<List<EquipmentResponse>> GetEquipmentAsync();

    Task<EquipmentResponse?> GetEquipmentByIdAsync(Guid id);

    Task<Guid> CreateEquipmentAsync(CreateEquipmentRequest request);

    Task UpdateEquipmentAsync(Guid id, UpdateEquipmentRequest request);

    Task<List<EquipmentAssignmentResponse>> GetAssignmentsAsync();

    Task<Guid> CreateAssignmentAsync(
        CreateEquipmentAssignmentRequest request);

    Task<EquipmentAssignmentResponse> ReleaseAssignmentAsync(
        Guid assignmentId,
        ReleaseEquipmentAssignmentRequest request);

    Task<List<EquipmentDowntimeResponse>> GetDowntimeAsync(
        Guid? equipmentId = null,
        Guid? constructionSiteId = null,
        bool? openOnly = null);

    Task<Guid> CreateDowntimeAsync(
        CreateEquipmentDowntimeRequest request);

        Task<EquipmentDowntimeResponse> UpdateDowntimeAsync(
            Guid downtimeId,
            UpdateEquipmentDowntimeRequest request);

            Task<EquipmentDowntimeResponse> CloseDowntimeAsync(
                Guid downtimeId,
                CloseEquipmentDowntimeRequest request);

        Task<List<EquipmentUtilizationResponse>> GetUtilizationAsync();

        Task<List<EquipmentMaintenanceResponse>> GetMaintenanceAsync(
            Guid? equipmentId = null,
            bool? completedOnly = null);

        Task<EquipmentMaintenanceResponse?> GetMaintenanceByIdAsync(
            Guid maintenanceId);

        Task<Guid> CreateMaintenanceAsync(
            CreateEquipmentMaintenanceRequest request);

        Task<EquipmentMaintenanceResponse> UpdateMaintenanceAsync(
            Guid maintenanceId,
            UpdateEquipmentMaintenanceRequest request);

        Task<List<EquipmentFuelLogResponse>> GetFuelLogsAsync(
            Guid? equipmentId = null);

        Task<Guid> CreateFuelLogAsync(
            CreateEquipmentFuelLogRequest request);

        Task<EquipmentFuelLogResponse?> GetFuelLogByIdAsync(Guid fuelLogId);

        Task UpdateFuelLogAsync(
            Guid fuelLogId,
            UpdateEquipmentFuelLogRequest request);

        Task<List<EquipmentHourLogResponse>> GetHourLogsAsync(
            Guid? equipmentId = null);

        Task<Guid> CreateHourLogAsync(
            CreateEquipmentHourLogRequest request);

        Task<List<EquipmentTimesheetResponse>> GetTimesheetsAsync(
            Guid? equipmentId = null);

        Task<Guid> CreateTimesheetAsync(
            CreateEquipmentTimesheetRequest request);

        Task<EquipmentTimesheetResponse> GetTimesheetByIdAsync(Guid id);

        Task UpdateTimesheetAsync(
            Guid id,
            CreateEquipmentTimesheetRequest request);
}