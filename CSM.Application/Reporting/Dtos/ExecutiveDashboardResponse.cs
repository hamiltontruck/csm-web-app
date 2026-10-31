using System.Text.Json.Serialization;
using CSM.Domain.Enums;

namespace CSM.Application.Reporting.Dtos;

public sealed record ExecutiveDashboardResponse(
    int TotalSites,
    int ActiveSites,
    int TotalProjects,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? TotalContractValue,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? TotalBudget,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? TotalExpenses,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? TotalInvoices,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? TotalCompletedPayments,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? OutstandingInvoices,
    IReadOnlyDictionary<SiteStatus, int> SitesByStatus,
    IReadOnlyDictionary<ProjectStatus, int> ProjectsByStatus);
