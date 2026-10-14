using System.ComponentModel.DataAnnotations;

namespace CSM.Application.Procurement.RebarSpecs.Dtos;

public sealed class CreateRebarSpecRequest
{
    public Guid MaterialId { get; set; }

    public decimal DiameterMm { get; set; }

    public string Grade { get; set; } = string.Empty;
}
