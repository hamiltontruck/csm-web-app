using CSM.Domain.Enums;

namespace CSM.Application.Finance.Budgets.Dtos;

public sealed record BudgetResponse
{
    public Guid Id { get; init; }
    public Guid CompanyId { get; init; }

    public Guid? ConstructionSiteId { get; set; }
    public string ConstructionSiteName { get; init; } = string.Empty;

    public Guid? ProjectId { get; init; }
    public string? ProjectName { get; init; }

    public string BudgetNumber { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }

    public decimal TotalAmount { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;

    public DateOnly EffectiveFrom { get; init; }
    public DateOnly? EffectiveTo { get; init; }

    public BudgetStatus Status { get; init; }

    public Guid? ApprovedBy { get; init; }
    public DateTime? ApprovedAtUtc { get; init; }

    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
}