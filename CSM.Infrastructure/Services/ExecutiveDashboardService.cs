using CSM.Application.Dashboard;
using CSM.Application.Dashboard.Dtos;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public sealed class ExecutiveDashboardService
    : IExecutiveDashboardService
{
    private readonly ApplicationDbContext _context;

    public ExecutiveDashboardService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ExecutiveDashboardResponse> GetAsync(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var activeProjects = await _context.Projects
            .CountAsync(x =>
                x.CompanyId == companyId &&
                x.Status != Domain.Enums.ProjectStatus.Completed,
                cancellationToken);

        var completedProjects = await _context.Projects
            .CountAsync(x =>
                x.CompanyId == companyId &&
                x.Status == Domain.Enums.ProjectStatus.Completed,
                cancellationToken);

        var totalBudget = await _context.Budgets
            .Where(x => x.CompanyId == companyId)
            .SumAsync(x => (decimal?)x.TotalAmount ?? 0,
                cancellationToken);

        var totalExpenses = await _context.Expenses
            .Where(x => x.CompanyId == companyId)
            .SumAsync(x => (decimal?)x.Amount ?? 0,
                cancellationToken);

        var budgetUtilization = totalBudget == 0
            ? 0
            : Math.Round((totalExpenses / totalBudget) * 100, 2);

        var openDelays = await _context.ProjectDelays
            .CountAsync(x =>
                x.CompanyId == companyId &&
                !x.IsResolved,
                cancellationToken);

        var totalEmployees = await _context.Employees
            .CountAsync(x =>
                x.CompanyId == companyId,
                cancellationToken);

        var lowStockItems = await _context.StockItems
            .CountAsync(x =>
                x.CompanyId == companyId &&
                x.QuantityOnHand <= x.ReorderLevel,
                cancellationToken);

        var openSafetyIncidents = await _context.SafetyIncidents
            .CountAsync(x =>
                x.CompanyId == companyId &&
                x.Status != Domain.Enums.SafetyIncidentStatus.Resolved,
                cancellationToken);

        return new ExecutiveDashboardResponse(
            activeProjects,
            completedProjects,
            totalBudget,
            totalExpenses,
            budgetUtilization,
            openDelays,
            totalEmployees,
            lowStockItems,
            openSafetyIncidents,
            DateTime.UtcNow);
    }
}