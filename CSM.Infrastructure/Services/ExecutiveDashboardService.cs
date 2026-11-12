using CSM.Infrastructure.Security;
using CSM.Domain.Enums;
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

    public async Task<ExecutiveDashboardResponse> GetForActorAsync(Guid actorId, Guid companyId, CancellationToken cancellationToken = default)
    {
        var actor = await _context.Users.AsNoTracking().Include(x => x.UserRoles).ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(x => x.Id == actorId && x.IsActive && !x.IsDeleted, cancellationToken)
            ?? throw new UnauthorizedAccessException("Authenticated user was not found.");
        var permissions = new PermissionEvaluator(_context);
        if (!await permissions.HasPermissionAsync(actorId, "Reports_View", cancellationToken) ||
            !await permissions.HasPermissionAsync(actorId, "Reports_FinancialSummary_View", cancellationToken))
            throw new UnauthorizedAccessException("Dashboard access is not permitted.");
        actor = await FinanceOfficerScope.ResolveActorAsync(_context, actorId, GranularPermission.Reports_FinancialSummary_View,
            cancellationToken, message => new UnauthorizedAccessException(message)) ?? actor;
        var superAdmin = actor.UserRoles.Any(x => !x.IsDeleted && !x.Role.IsDeleted && x.Role.Name == "SuperAdmin");
        if (!superAdmin && actor.CompanyId != companyId) throw new UnauthorizedAccessException("Company is outside your scope.");
        var scope = new FinancialReadScope(_context, actor, companyId);
        if (scope.IncludesCompany) return await GetAsync(companyId, cancellationToken);
        var budget = await scope.Finance(_context.Budgets).SumAsync(x => (decimal?)x.TotalAmount, cancellationToken) ?? 0m;
        var expenses = await scope.Finance(_context.Expenses).SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        return new ExecutiveDashboardResponse(0, 0, budget, expenses, budget == 0 ? 0 : Math.Round(expenses / budget * 100, 2), 0, 0, 0, 0, DateTime.UtcNow);
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