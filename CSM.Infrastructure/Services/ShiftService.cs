using CSM.Application.Common.Exceptions;
using CSM.Application.HRM.Shifts;
using CSM.Application.HRM.Shifts.Dtos;
using CSM.Domain.Constants;
using CSM.Domain.Entities.HRM;
using CSM.Domain.Entities.Identity;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public sealed class ShiftService : IShiftService
{
    private readonly ApplicationDbContext _dbContext;

    public ShiftService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<ShiftResponse>> GetAllAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanRead(actor);

        IQueryable<Shift> query =
            _dbContext.Shifts.AsNoTracking();

        if (!IsSuperAdmin(actor))
        {
            if (!actor.CompanyId.HasValue)
            {
                throw new ShiftManagementException(
                    "Current user is not assigned to a company.");
            }

            query = query.Where(
                x => x.CompanyId == actor.CompanyId.Value);
        }

        var shifts = await query
            .OrderBy(x => x.Code)
            .ToListAsync(cancellationToken);

        return shifts
            .Select(Map)
            .ToArray();
    }

    public async Task<ShiftResponse> GetByIdAsync(
        Guid currentUserId,
        Guid shiftId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanRead(actor);

        var shift = await GetShiftAsync(
            shiftId,
            cancellationToken);

        EnsureCompanyAccess(actor, shift.CompanyId);

        return Map(shift);
    }

    public async Task<ShiftResponse> CreateAsync(
        Guid currentUserId,
        CreateShiftRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanModify(actor);

        var companyId = GetActorCompanyId(actor);

        var code = CleanRequired(
            request.Code,
            "Shift code");

        var name = CleanRequired(
            request.Name,
            "Shift name");

        ValidateShift(
            request.StartTime,
            request.EndTime,
            request.GracePeriodMinutes,
            request.StandardHours,
            request.IsNightShift);

        var duplicateExists = await _dbContext.Shifts
            .AnyAsync(
                x =>
                    x.CompanyId == companyId &&
                    x.Code == code,
                cancellationToken);

        if (duplicateExists)
        {
            throw new ShiftManagementException(
                $"Shift code '{code}' already exists.");
        }

        var shift = new Shift
        {
            CompanyId = companyId,
            Code = code,
            Name = name,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            GracePeriodMinutes =
                request.GracePeriodMinutes,
            StandardHours =
                request.StandardHours,
            IsNightShift =
                request.IsNightShift,
            IsActive = true
        };

        _dbContext.Shifts.Add(shift);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(shift);
    }

    public async Task<ShiftResponse> UpdateAsync(
        Guid currentUserId,
        Guid shiftId,
        UpdateShiftRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanModify(actor);

        var shift = await GetShiftAsync(
            shiftId,
            cancellationToken);

        EnsureCompanyAccess(actor, shift.CompanyId);

        var name = CleanRequired(
            request.Name,
            "Shift name");

        ValidateShift(
            request.StartTime,
            request.EndTime,
            request.GracePeriodMinutes,
            request.StandardHours,
            request.IsNightShift);

        shift.Name = name;
        shift.StartTime = request.StartTime;
        shift.EndTime = request.EndTime;
        shift.GracePeriodMinutes =
            request.GracePeriodMinutes;
        shift.StandardHours =
            request.StandardHours;
        shift.IsNightShift =
            request.IsNightShift;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(shift);
    }

    public async Task<ShiftResponse> ChangeActiveStatusAsync(
        Guid currentUserId,
        Guid shiftId,
        ChangeShiftActiveStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanModify(actor);

        var shift = await GetShiftAsync(
            shiftId,
            cancellationToken);

        EnsureCompanyAccess(actor, shift.CompanyId);

        shift.IsActive = request.IsActive;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(shift);
    }

    private async Task<User> GetActorAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var actor = await _dbContext.Users
            .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(
                x => x.Id == userId,
                cancellationToken);

        if (actor is null || !actor.IsActive)
        {
            throw new ShiftManagementException(
                "Current user was not found or is inactive.");
        }

        return actor;
    }

    private async Task<Shift> GetShiftAsync(
        Guid shiftId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Shifts
            .SingleOrDefaultAsync(
                x => x.Id == shiftId,
                cancellationToken)
            ?? throw new ShiftManagementException(
                "Shift was not found.");
    }

    private static void EnsureCanRead(User actor)
    {
        if (IsSuperAdmin(actor))
        {
            return;
        }

        if (HasRole(actor, AppRoles.CompanyAdmin) ||
            HasRole(actor, AppRoles.SiteManager) ||
            HasRole(actor, AppRoles.Supervisor) ||
            HasRole(actor, AppRoles.HrOfficer) ||
            HasRole(actor, AppRoles.HROfficer))
        {
            return;
        }

        throw new ShiftManagementException(
            "You are not authorized to access shifts.");
    }

    private static void EnsureCanModify(User actor)
    {
        if (IsSuperAdmin(actor) ||
            HasRole(actor, AppRoles.CompanyAdmin) ||
            HasRole(actor, AppRoles.HrOfficer) ||
            HasRole(actor, AppRoles.HROfficer))
        {
            return;
        }

        throw new ShiftManagementException(
            "You are not authorized to create or modify shifts.");
    }

    private static Guid GetActorCompanyId(User actor)
    {
        if (!actor.CompanyId.HasValue)
        {
            throw new ShiftManagementException(
                "Current user is not assigned to a company.");
        }

        return actor.CompanyId.Value;
    }

    private static void EnsureCompanyAccess(
        User actor,
        Guid companyId)
    {
        if (IsSuperAdmin(actor))
        {
            return;
        }

        if (!actor.CompanyId.HasValue ||
            actor.CompanyId.Value != companyId)
        {
            throw new ShiftManagementException(
                "You cannot access shifts outside your company.");
        }
    }

    private static void ValidateShift(
        TimeOnly startTime,
        TimeOnly endTime,
        int gracePeriodMinutes,
        decimal standardHours,
        bool isNightShift)
    {
        if (gracePeriodMinutes < 0)
        {
            throw new ShiftManagementException(
                "Grace period cannot be negative.");
        }

        if (standardHours <= 0m ||
            standardHours > 24m)
        {
            throw new ShiftManagementException(
                "Standard hours must be greater than 0 and no more than 24.");
        }

        if (startTime == endTime)
        {
            throw new ShiftManagementException(
                "Shift start time and end time cannot be the same.");
        }

        if (!isNightShift &&
            endTime <= startTime)
        {
            throw new ShiftManagementException(
                "A normal shift must end after it starts.");
        }

        if (isNightShift &&
            endTime > startTime)
        {
            throw new ShiftManagementException(
                "A night shift must cross midnight.");
        }
    }

    private static string CleanRequired(
        string? value,
        string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ShiftManagementException(
                $"{fieldName} is required.");
        }

        return value.Trim();
    }

    private static bool IsSuperAdmin(User actor)
    {
        return HasRole(
            actor,
            AppRoles.SuperAdmin);
    }

    private static bool HasRole(
        User actor,
        string roleName)
    {
        return actor.UserRoles.Any(
            x =>
                !x.IsDeleted &&
                !x.Role.IsDeleted &&
                string.Equals(
                    x.Role.Name,
                    roleName,
                    StringComparison.OrdinalIgnoreCase));
    }

    private static ShiftResponse Map(Shift shift)
    {
        return new ShiftResponse(
            shift.Id,
            shift.CompanyId,
            shift.Code,
            shift.Name,
            shift.StartTime,
            shift.EndTime,
            shift.GracePeriodMinutes,
            shift.StandardHours,
            shift.IsNightShift,
            shift.IsActive,
            shift.CreatedAtUtc);
    }
}