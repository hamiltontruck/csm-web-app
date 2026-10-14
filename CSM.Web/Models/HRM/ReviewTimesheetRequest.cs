namespace CSM.Web.Models.HRM;

public sealed class ReviewTimesheetRequest
{
    public string Status { get; set; } = string.Empty;

    public string? Remarks { get; set; }
}