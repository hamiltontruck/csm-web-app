namespace CSM.Web.Models.Equipment;

public class EquipmentAssignmentResponse
{
    public Guid Id { get; set; }

    public string AssignmentNumber { get; set; } = "";

    public Guid CompanyId { get; set; }

    public Guid EquipmentId { get; set; }
    public string EquipmentCode { get; set; } = "";
    public string EquipmentName { get; set; } = "";

    public Guid ConstructionSiteId { get; set; }
    public string ConstructionSiteName { get; set; } = "";

    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }

    public DateTime AssignedAtUtc { get; set; }
    public DateTime? ReleasedAtUtc { get; set; }

    public decimal? MeterReadingAtAssignment { get; set; }
    public decimal? MeterReadingAtRelease { get; set; }

    public string? Remarks { get; set; }

    public bool IsActive => !ReleasedAtUtc.HasValue;
}