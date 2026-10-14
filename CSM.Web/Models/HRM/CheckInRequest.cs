namespace CSM.Web.Models.HRM;

public sealed class CheckInRequest
{
    public Guid EmployeeId { get; set; }
    public Guid ConstructionSiteId { get; set; }
    public Guid? ShiftId { get; set; }

    public DateTime CheckInAtUtc { get; set; }

    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public decimal? AccuracyMeters { get; set; }

    public string Source { get; set; } = string.Empty;

    public string? BiometricReference { get; set; }
    public string? Remarks { get; set; }
}