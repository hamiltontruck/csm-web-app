using CSM.Infrastructure.Security;
using CSM.Domain.Enums;
using CSM.Domain.Security;
using CSM.Application.Common.Exceptions;
using CSM.Application.Procurement.EquipmentTimesheets;
using CSM.Domain.Entities.Equipment;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public sealed class EquipmentTimesheetService
{
    private readonly ApplicationDbContext _db;

    public EquipmentTimesheetService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<EquipmentTimesheetResponse>> GetForUserAsync(
        Guid userId, Guid? equipmentId, CancellationToken cancellationToken = default)
    {
        var company = await ResolveCompanyScopeAsync(userId, false, cancellationToken);
        return await QueryAsync(company, equipmentId, cancellationToken);
    }

    public async Task<EquipmentTimesheetResponse> GetByIdForUserAsync(
        Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var company = await ResolveCompanyScopeAsync(userId, false, cancellationToken);
        var row = await GetScopedRowAsync(company, id, cancellationToken);
        return await GetByIdAsync(row.CompanyId, id, cancellationToken);
    }

    public async Task<EquipmentTimesheetResponse> CreateForUserAsync(
        Guid userId, CreateEquipmentTimesheetRequest request, CancellationToken cancellationToken = default)
    {
        var scope = await ResolveCompanyScopeAsync(userId, true, cancellationToken);
        var company = await ResolveResourceCompanyAsync(scope, null, request, cancellationToken);
        return await CreateAsync(company, request, cancellationToken);
    }

    public async Task<EquipmentTimesheetResponse> UpdateForUserAsync(
        Guid userId, Guid id, CreateEquipmentTimesheetRequest request, CancellationToken cancellationToken = default)
    {
        var scope = await ResolveCompanyScopeAsync(userId, true, cancellationToken);
        var row = await GetScopedRowAsync(scope, id, cancellationToken);
        await ResolveResourceCompanyAsync(scope, row.CompanyId, request, cancellationToken);
        return await UpdateAsync(row.CompanyId, id, request, cancellationToken);
    }

    private async Task<Guid?> ResolveCompanyScopeAsync(Guid userId, bool write, CancellationToken token)
    {
        var actor = await _db.Users.AsNoTracking()
            .Include(x => x.UserRoles).ThenInclude(x => x.Role).ThenInclude(x => x.GranularPermissions)
            .SingleOrDefaultAsync(x => x.Id == userId && x.IsActive && !x.IsDeleted, token)
            ?? throw new EquipmentManagementException("Current user was not found or is inactive.");
        var roles = actor.UserRoles.Where(x => !x.IsDeleted && !x.Role.IsDeleted && x.Role.Name != "ProcurementOfficer")
            .Select(x => x.Role).ToArray();
        bool Has(GranularPermission permission) => roles.Any(role =>
            RolePermissionRules.Allows(role.Name, permission.ToString()) && role.GranularPermissions.Any(x => x.Permission == permission));
        var view = GranularPermission.Procurement_Inventory_View;
        var adjust = GranularPermission.Procurement_Inventory_Adjust;
        if (!Has(view) || (write && !Has(adjust)))
            throw new EquipmentManagementException("You do not have permission to access equipment timesheets.");
        var required = write ? new[] { view, adjust } : new[] { view };
        if (await CompanyContextResolver.HasGlobalAuthorityAsync(_db, userId, required, token)) return null;
        if (!actor.CompanyId.HasValue || actor.CompanyId == Guid.Empty)
            throw new EquipmentManagementException("Current user is not assigned to a company.");
        return actor.CompanyId.Value;
    }

    private async Task<EquipmentTimesheet> GetScopedRowAsync(Guid? company, Guid id, CancellationToken token) =>
        await _db.EquipmentTimesheets.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted &&
            !x.Equipment.IsDeleted && (!company.HasValue || x.CompanyId == company), token)
        ?? throw new EquipmentManagementException("Timesheet not found.");

    private async Task<Guid> ResolveResourceCompanyAsync(Guid? scope, Guid? existingCompany,
        CreateEquipmentTimesheetRequest request, CancellationToken token)
    {
        var company = await _db.Equipment.AsNoTracking().Where(x => x.Id == request.EquipmentId && !x.IsDeleted &&
            (!scope.HasValue || x.CompanyId == scope) && (!existingCompany.HasValue || x.CompanyId == existingCompany))
            .Select(x => (Guid?)x.CompanyId).SingleOrDefaultAsync(token)
            ?? throw new EquipmentManagementException("Equipment was not found in the timesheet company.");
        if (!await _db.ConstructionSites.AsNoTracking().AnyAsync(x => x.Id == request.SiteId && !x.IsDeleted && x.CompanyId == company, token))
            throw new EquipmentManagementException("Construction site was not found in the equipment company.");
        return company;
    }

    // Trusted company-scoped compatibility methods; authenticated API uses the user-aware entry points above.
    public Task<IReadOnlyList<EquipmentTimesheetResponse>> GetAllAsync(Guid companyId, Guid? equipmentId,
        CancellationToken cancellationToken = default) => QueryAsync(companyId, equipmentId, cancellationToken);

    private async Task<IReadOnlyList<EquipmentTimesheetResponse>> QueryAsync(
        Guid? companyId,
        Guid? equipmentId,
        CancellationToken cancellationToken = default)
    {
        var query = _db.EquipmentTimesheets
            .AsNoTracking()
            .Include(x => x.Equipment)
            .Where(x => !x.IsDeleted && !x.Equipment.IsDeleted && (!companyId.HasValue || x.CompanyId == companyId));

        if (equipmentId.HasValue)
            query = query.Where(x => x.EquipmentId == equipmentId.Value);

        return await query
            .OrderByDescending(x => x.WorkDate)
            .Select(x => new EquipmentTimesheetResponse(
                x.Id,
                x.EquipmentId,
                x.Equipment.EquipmentCode,
                x.Equipment.Name,
                x.SiteId,
                "",
                x.WorkDate.ToDateTime(TimeOnly.MinValue),
                x.OperatorName,
                x.Shift,
                x.StartTime.ToTimeSpan(),
                x.EndTime.ToTimeSpan(),
                x.BreakMinutes,
                x.WorkedHours,
                x.Activity,
                x.Remarks))
            .ToListAsync(cancellationToken);
    }

    public async Task<EquipmentTimesheetResponse> CreateAsync(
        Guid companyId,
        CreateEquipmentTimesheetRequest request,
        CancellationToken cancellationToken = default)
    {
        var duration = request.EndTime - request.StartTime;

        if (duration < TimeSpan.Zero)
            duration = duration.Add(TimeSpan.FromDays(1));

        var workedHours = (decimal)duration.TotalHours
                        - (request.BreakMinutes / 60m);

        if (workedHours < 0)
            workedHours = 0;

        var entity = new EquipmentTimesheet
        {
            CompanyId = companyId,
            EquipmentId = request.EquipmentId,
            SiteId = request.SiteId,
            WorkDate = DateOnly.FromDateTime(request.WorkDate),
            OperatorName = request.OperatorName,
            Shift = request.Shift,
            StartTime = TimeOnly.FromTimeSpan(request.StartTime),
            EndTime = TimeOnly.FromTimeSpan(request.EndTime),
            BreakMinutes = request.BreakMinutes,
            WorkedHours = Math.Round(workedHours, 2),
            Activity = request.Activity,
            Remarks = request.Remarks
        };

        _db.EquipmentTimesheets.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        var equipment = await _db.Equipment
            .FirstAsync(x => x.Id == entity.EquipmentId, cancellationToken);

        return new EquipmentTimesheetResponse(
            entity.Id,
            entity.EquipmentId,
            equipment.EquipmentCode,
            equipment.Name,
            entity.SiteId,
            "",
            entity.WorkDate.ToDateTime(TimeOnly.MinValue),
            entity.OperatorName,
            entity.Shift,
            entity.StartTime.ToTimeSpan(),
            entity.EndTime.ToTimeSpan(),
            entity.BreakMinutes,
            entity.WorkedHours,
            entity.Activity,
            entity.Remarks);
    }

    public async Task<EquipmentTimesheetResponse> GetByIdAsync(
        Guid companyId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var item = await _db.EquipmentTimesheets
            .AsNoTracking()
            .Include(x => x.Equipment)
            .FirstOrDefaultAsync(
                x => x.CompanyId == companyId && x.Id == id,
                cancellationToken);

        if (item is null)
            throw new EquipmentManagementException("Timesheet not found.");

        return new EquipmentTimesheetResponse(
            item.Id,
            item.EquipmentId,
            item.Equipment.EquipmentCode,
            item.Equipment.Name,
            item.SiteId,
            "",
            item.WorkDate.ToDateTime(TimeOnly.MinValue),
            item.OperatorName,
            item.Shift,
            item.StartTime.ToTimeSpan(),
            item.EndTime.ToTimeSpan(),
            item.BreakMinutes,
            item.WorkedHours,
            item.Activity,
            item.Remarks);
    }

    public async Task<EquipmentTimesheetResponse> UpdateAsync(
        Guid companyId,
        Guid id,
        CreateEquipmentTimesheetRequest request,
        CancellationToken cancellationToken = default)
    {
        var item = await _db.EquipmentTimesheets
            .Include(x => x.Equipment)
            .FirstOrDefaultAsync(
                x => x.CompanyId == companyId && x.Id == id,
                cancellationToken);

        if (item is null)
            throw new EquipmentManagementException("Timesheet not found.");

        var duration = request.EndTime - request.StartTime;

        if (duration < TimeSpan.Zero)
            duration = duration.Add(TimeSpan.FromDays(1));

        var workedHours = (decimal)duration.TotalHours
                        - (request.BreakMinutes / 60m);

        if (workedHours < 0)
            workedHours = 0;

        item.WorkedHours = Math.Round(workedHours, 2);

        Console.WriteLine($"Duration={duration} Worked={item.WorkedHours}");

        item.Equipment = await _db.Equipment.SingleAsync(x => x.Id == request.EquipmentId, cancellationToken);
        item.EquipmentId = request.EquipmentId;
        item.SiteId = request.SiteId;
        item.WorkDate = DateOnly.FromDateTime(request.WorkDate);
        item.OperatorName = request.OperatorName;
        item.Shift = request.Shift;
        item.StartTime = TimeOnly.FromTimeSpan(request.StartTime);
        item.EndTime = TimeOnly.FromTimeSpan(request.EndTime);
        item.BreakMinutes = request.BreakMinutes;
        item.Activity = request.Activity;
        item.Remarks = request.Remarks;

        item.WorkedHours = Math.Round(workedHours, 2);

        Console.WriteLine(
            $"Duration={duration} Worked={item.WorkedHours}");

        await _db.SaveChangesAsync(cancellationToken);

        return new EquipmentTimesheetResponse(
            item.Id,
            item.EquipmentId,
            item.Equipment.EquipmentCode,
            item.Equipment.Name,
            item.SiteId,
            "",
            item.WorkDate.ToDateTime(TimeOnly.MinValue),
            item.OperatorName,
            item.Shift,
            item.StartTime.ToTimeSpan(),
            item.EndTime.ToTimeSpan(),
            item.BreakMinutes,
            item.WorkedHours,
            item.Activity,
            item.Remarks);
    }
}