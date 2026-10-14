namespace CSM.Web.Models.HRM;

public sealed class ReviewLeaveRequest
{
    public string Status { get; set; } = string.Empty;

    public string? Remarks { get; set; }
}