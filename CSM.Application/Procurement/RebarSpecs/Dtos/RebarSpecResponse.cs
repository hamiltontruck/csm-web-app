namespace CSM.Application.Procurement.RebarSpecs.Dtos;

public sealed class RebarSpecResponse
{
    public Guid Id { get; set; }

    public Guid MaterialId { get; set; }

    public string MaterialCode { get; set; } = string.Empty;

    public string MaterialName { get; set; } = string.Empty;

    public decimal DiameterMm { get; set; }

    public string Grade { get; set; } = string.Empty;
}
