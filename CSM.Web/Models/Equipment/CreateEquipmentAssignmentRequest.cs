using System.ComponentModel.DataAnnotations;

namespace CSM.Web.Models.Equipment;

public class CreateEquipmentAssignmentRequest
{
    [Required]
    public Guid EquipmentId { get; set; }

    [Required]
    public Guid ConstructionSiteId { get; set; }

    public Guid? ProjectId { get; set; }

    [Required]
    public DateTime AssignedAtUtc { get; set; }

    public decimal? MeterReadingAtAssignment { get; set; }

    public string? Remarks { get; set; }
}