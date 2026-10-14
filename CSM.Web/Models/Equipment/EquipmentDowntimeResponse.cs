namespace CSM.Web.Models.Equipment;

public class EquipmentDowntimeResponse
{
    public Guid Id { get; set; }

    public Guid CompanyId { get; set; }

    public Guid EquipmentId { get; set; }

    public string EquipmentCode { get; set; } = string.Empty;

    public string EquipmentName { get; set; } = string.Empty;

    public Guid? ConstructionSiteId { get; set; }

    public string? ConstructionSiteCode { get; set; }

    public string? ConstructionSiteName { get; set; }

    public DateTime StartedAtUtc { get; set; }

    public DateTime? EndedAtUtc { get; set; }

    public string DowntimeNumber { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public string? Resolution { get; set; }

    public double? DurationHours { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public bool IsOpen => !EndedAtUtc.HasValue;
}