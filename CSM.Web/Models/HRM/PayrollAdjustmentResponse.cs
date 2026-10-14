namespace CSM.Web.Models.HRM;

public sealed class PayrollAdjustmentResponse
{
    public Guid Id { get; set; }

    public string Type { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public bool IsDeduction { get; set; }
}