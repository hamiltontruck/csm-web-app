namespace CSM.Web.Models.HRM;

public sealed class OverrideAttendanceRequest
{
    public DateTime? CheckInAtUtc { get; set; }

    public DateTime? CheckOutAtUtc { get; set; }

    public string Status { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public string? Remarks { get; set; }
}