namespace CSM.Web.Models.Sites;

public class UpdateSiteRequest
{
    public string Name { get; set; } = "";

    public string? Description { get; set; }

    public Guid? BusinessUnitId { get; set; }

    public string? Address { get; set; }

    public string? City { get; set; }

    public string? Region { get; set; }

    public string? Country { get; set; }

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public decimal? GeofenceRadiusMeters { get; set; }

    public DateOnly? PlannedStartDate { get; set; }

    public DateOnly? PlannedEndDate { get; set; }
}