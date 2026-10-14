namespace CSM.Web.Models.HRM;

public sealed class CheckOutRequest
{
    public DateTime CheckOutAtUtc { get; set; }

    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public decimal? AccuracyMeters { get; set; }

    public string? Remarks { get; set; }
}