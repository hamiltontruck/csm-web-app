namespace CSM.Application.Procurement.RebarSpecs.Dtos;

public sealed class UpdateRebarSpecRequest
{
    public decimal DiameterMm { get; set; }

    public string Grade { get; set; } = string.Empty;
}
