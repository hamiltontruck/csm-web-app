namespace CSM.Web.Models.Finance;

public sealed class UpdateBudgetModel
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }

    public decimal TotalAmount { get; set; }

    public string CurrencyCode { get; set; } = "ETB";

    public DateOnly? EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
}