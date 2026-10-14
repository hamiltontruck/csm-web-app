using System.Text.Json.Serialization;

namespace CSM.Web.Models.Sites;

public class SiteResponse
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }

    public string SiteCode { get; set; } = "";
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
    public DateOnly? ActualStartDate { get; set; }
    public DateOnly? ActualEndDate { get; set; }

    public string Status { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}