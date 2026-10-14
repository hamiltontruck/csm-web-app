namespace CSM.Web.Models.HRM;

public sealed class CreatePayrollAdjustmentRequest
{
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public bool IsDeduction { get; set; }
}