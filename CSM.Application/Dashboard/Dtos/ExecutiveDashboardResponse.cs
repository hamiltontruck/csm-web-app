using System;

namespace CSM.Application.Dashboard.Dtos;

public sealed record ExecutiveDashboardResponse(
    int ActiveProjects,
    int CompletedProjects,
    decimal TotalBudget,
    decimal TotalExpenses,
    decimal BudgetUtilization,
    int OpenDelays,
    int TotalEmployees,
    int LowStockItems,
    int OpenSafetyIncidents,
    DateTime GeneratedAtUtc
);