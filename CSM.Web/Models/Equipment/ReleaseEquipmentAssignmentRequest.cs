namespace CSM.Web.Models.Equipment;

public class ReleaseEquipmentAssignmentRequest
{
    public DateTime ReleasedAtUtc { get; set; }

    public decimal? MeterReadingAtRelease { get; set; }
}