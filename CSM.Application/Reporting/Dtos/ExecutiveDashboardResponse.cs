using CSM.Domain.Enums;

namespace CSM.Application.Reporting.Dtos;

public sealed record ExecutiveDashboardResponse(
    int TotalSites,
    int ActiveSites,
    int TotalProjects,
    decimal TotalContractValue,
    decimal TotalBudget,
    decimal TotalExpenses,
    decimal TotalInvoices,
    decimal TotalCompletedPayments,
    decimal OutstandingInvoices,
    IReadOnlyDictionary<SiteStatus, int> SitesByStatus,
    IReadOnlyDictionary<ProjectStatus, int> ProjectsByStatus);
