using CSM.Application.Common.Exceptions;
using CSM.Application.HRM.Employees;
using CSM.Application.HRM.Employees.Dtos;
using CSM.Domain.Constants;
using CSM.Domain.Entities.HRM;
using CSM.Domain.Entities.Identity;
using CSM.Domain.Enums;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public sealed class EmployeeService : IEmployeeService
{
    private readonly ApplicationDbContext _dbContext;

    public EmployeeService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<EmployeeResponse>> GetAllAsync(
        Guid currentUserId,
        EmployeeStatus? status = null,
        EmployeeScope? employeeScope = null,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanRead(actor);

        IQueryable<Employee> query =
            _dbContext.Employees.AsNoTracking();

        if (!IsSuperAdmin(actor))
        {
            var companyId = GetActorCompanyId(actor);

            query = query.Where(
                x => x.CompanyId == companyId);
        }

        if (status.HasValue)
        {
            query = query.Where(
                x => x.Status == status.Value);
        }

        if (employeeScope.HasValue)
        {
            query = query.Where(
                x => x.EmployeeScope == employeeScope.Value);
        }

        var employees = await query
            .OrderBy(x => x.EmployeeNumber)
            .ToListAsync(cancellationToken);

        return employees
            .Select(Map)
            .ToArray();
    }

    public async Task<EmployeeResponse> GetByIdAsync(
        Guid currentUserId,
        Guid employeeId,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanRead(actor);

        var employee = await GetEmployeeAsync(
            employeeId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            employee.CompanyId);

        return Map(employee);
    }

    public async Task<EmployeeResponse> CreateAsync(
        Guid currentUserId,
        CreateEmployeeRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanModify(actor);

        var companyId = GetActorCompanyId(actor);

        var employeeNumber = CleanRequired(
            request.EmployeeNumber,
            "Employee number");

        var firstName = CleanRequired(
            request.FirstName,
            "First name");

        var lastName = CleanRequired(
            request.LastName,
            "Last name");

        var currencyCode = NormalizeCurrencyCode(
            request.CurrencyCode);

        ValidateEmployeeData(
            request.DateOfBirth,
            request.HireDate,
            request.BaseWage,
            currencyCode);

        var duplicateExists =
            await _dbContext.Employees.AnyAsync(
                x =>
                    x.CompanyId == companyId &&
                    x.EmployeeNumber == employeeNumber,
                cancellationToken);

        if (duplicateExists)
        {
            throw new EmployeeManagementException(
                $"Employee number '{employeeNumber}' already exists.");
        }

        var employee = new Employee
        {
            CompanyId = companyId,
            EmployeeNumber = employeeNumber,
            FirstName = firstName,
            MiddleName = Clean(request.MiddleName),
            LastName = lastName,
            PhoneNumber = Clean(request.PhoneNumber),
            Email = NormalizeEmail(request.Email),
            NationalIdNumber =
                Clean(request.NationalIdNumber),
            TaxIdentificationNumber =
                Clean(request.TaxIdentificationNumber),
            DateOfBirth = request.DateOfBirth,
            HireDate = request.HireDate,
            TerminationDate = null,
            JobTitle = Clean(request.JobTitle),
            Department = Clean(request.Department),
            EmployeeType = request.EmployeeType,
            EmployeeScope = request.EmployeeScope,
            Status = EmployeeStatus.Active,
            WageType = request.WageType,
            BaseWage = request.BaseWage,
            CurrencyCode = currencyCode,
            BankName = Clean(request.BankName),
            BankAccountNumber =
                Clean(request.BankAccountNumber),
            EmergencyContactName =
                Clean(request.EmergencyContactName),
            EmergencyContactPhone =
                Clean(request.EmergencyContactPhone)
        };

        _dbContext.Employees.Add(employee);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(employee);
    }

    public async Task<EmployeeResponse> UpdateAsync(
        Guid currentUserId,
        Guid employeeId,
        UpdateEmployeeRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanModify(actor);

        var employee = await GetEmployeeAsync(
            employeeId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            employee.CompanyId);

        if (employee.Status == EmployeeStatus.Terminated)
        {
            throw new EmployeeManagementException(
                "A terminated employee cannot be modified.");
        }

        var firstName = CleanRequired(
            request.FirstName,
            "First name");

        var lastName = CleanRequired(
            request.LastName,
            "Last name");

        var currencyCode = NormalizeCurrencyCode(
            request.CurrencyCode);

        ValidateEmployeeData(
            request.DateOfBirth,
            request.HireDate,
            request.BaseWage,
            currencyCode);

        employee.FirstName = firstName;
        employee.MiddleName =
            Clean(request.MiddleName);
        employee.LastName = lastName;
        employee.PhoneNumber =
            Clean(request.PhoneNumber);
        employee.Email =
            NormalizeEmail(request.Email);
        employee.NationalIdNumber =
            Clean(request.NationalIdNumber);
        employee.TaxIdentificationNumber =
            Clean(request.TaxIdentificationNumber);
        employee.DateOfBirth =
            request.DateOfBirth;
        employee.HireDate =
            request.HireDate;
        employee.JobTitle =
            Clean(request.JobTitle);
        employee.Department =
            Clean(request.Department);
        employee.EmployeeType =
            request.EmployeeType;
        employee.EmployeeScope =
            request.EmployeeScope;
        employee.WageType =
            request.WageType;
        employee.BaseWage =
            request.BaseWage;
        employee.CurrencyCode =
            currencyCode;
        employee.BankName =
            Clean(request.BankName);
        employee.BankAccountNumber =
            Clean(request.BankAccountNumber);
        employee.EmergencyContactName =
            Clean(request.EmergencyContactName);
        employee.EmergencyContactPhone =
            Clean(request.EmergencyContactPhone);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(employee);
    }

    public async Task<EmployeeResponse> ChangeStatusAsync(
        Guid currentUserId,
        Guid employeeId,
        ChangeEmployeeStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await GetActorAsync(
            currentUserId,
            cancellationToken);

        EnsureCanModify(actor);

        var employee = await GetEmployeeAsync(
            employeeId,
            cancellationToken);

        EnsureCompanyAccess(
            actor,
            employee.CompanyId);

        ValidateStatusTransition(
            employee.Status,
            request.Status);

        if (request.Status == EmployeeStatus.Terminated)
        {
            if (!request.TerminationDate.HasValue)
            {
                throw new EmployeeManagementException(
                    "Termination date is required when terminating an employee.");
            }

            if (request.TerminationDate.Value <
                employee.HireDate)
            {
                throw new EmployeeManagementException(
                    "Termination date cannot be earlier than hire date.");
            }

            employee.TerminationDate =
                request.TerminationDate.Value;
        }
        else
        {
            employee.TerminationDate = null;
        }

        employee.Status = request.Status;

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(employee);
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
            throw new EmployeeManagementException(
                "Current user was not found or is inactive.");
        }

        return actor;
    }

    private async Task<Employee> GetEmployeeAsync(
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Employees
            .SingleOrDefaultAsync(
                x => x.Id == employeeId,
                cancellationToken)
            ?? throw new EmployeeManagementException(
                "Employee was not found.");
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

        throw new EmployeeManagementException(
            "You are not authorized to access employees.");
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

        throw new EmployeeManagementException(
            "You are not authorized to create or modify employees.");
    }

    private static Guid GetActorCompanyId(User actor)
    {
        if (!actor.CompanyId.HasValue)
        {
            throw new EmployeeManagementException(
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
            throw new EmployeeManagementException(
                "You cannot access employees outside your company.");
        }
    }

    private static void ValidateEmployeeData(
        DateOnly? dateOfBirth,
        DateOnly hireDate,
        decimal baseWage,
        string currencyCode)
    {
        var today = DateOnly.FromDateTime(
            DateTime.UtcNow);

        if (hireDate > today)
        {
            throw new EmployeeManagementException(
                "Hire date cannot be in the future.");
        }

        if (dateOfBirth.HasValue)
        {
            if (dateOfBirth.Value >= today)
            {
                throw new EmployeeManagementException(
                    "Date of birth must be earlier than today.");
            }

            if (dateOfBirth.Value >= hireDate)
            {
                throw new EmployeeManagementException(
                    "Date of birth must be earlier than hire date.");
            }
        }

        if (baseWage < 0m)
        {
            throw new EmployeeManagementException(
                "Base wage cannot be negative.");
        }

        if (currencyCode.Length != 3)
        {
            throw new EmployeeManagementException(
                "Currency code must contain exactly three characters.");
        }
    }

    private static void ValidateStatusTransition(
        EmployeeStatus current,
        EmployeeStatus next)
    {
        if (current == next)
        {
            return;
        }

        var allowed = current switch
        {
            EmployeeStatus.Active =>
                next is
                    EmployeeStatus.OnLeave or
                    EmployeeStatus.Suspended or
                    EmployeeStatus.Inactive or
                    EmployeeStatus.Terminated,

            EmployeeStatus.OnLeave =>
                next is
                    EmployeeStatus.Active or
                    EmployeeStatus.Terminated,

            EmployeeStatus.Suspended =>
                next is
                    EmployeeStatus.Active or
                    EmployeeStatus.Inactive or
                    EmployeeStatus.Terminated,

            EmployeeStatus.Inactive =>
                next is
                    EmployeeStatus.Active or
                    EmployeeStatus.Terminated,

            EmployeeStatus.Terminated => false,

            _ => false
        };

        if (!allowed)
        {
            throw new EmployeeManagementException(
                $"Employee status transition from {current} to {next} is not allowed.");
        }
    }

    private static string NormalizeCurrencyCode(
        string? value)
    {
        var currencyCode = CleanRequired(
            value,
            "Currency code")
            .ToUpperInvariant();

        if (currencyCode.Length != 3 ||
            !currencyCode.All(char.IsLetter))
        {
            throw new EmployeeManagementException(
                "Currency code must be a three-letter code.");
        }

        return currencyCode;
    }

    private static string? NormalizeEmail(
        string? value)
    {
        var email = Clean(value);

        return email?.ToLowerInvariant();
    }

    private static string CleanRequired(
        string? value,
        string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new EmployeeManagementException(
                $"{fieldName} is required.");
        }

        return value.Trim();
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
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

    private static EmployeeResponse Map(
        Employee employee)
    {
        return new EmployeeResponse(
            employee.Id,
            employee.CompanyId,
            employee.EmployeeNumber,
            employee.FirstName,
            employee.MiddleName,
            employee.LastName,
            employee.PhoneNumber,
            employee.Email,
            employee.NationalIdNumber,
            employee.TaxIdentificationNumber,
            employee.DateOfBirth,
            employee.HireDate,
            employee.TerminationDate,
            employee.JobTitle,
            employee.Department,
            employee.EmployeeType,
            employee.EmployeeScope,
            employee.Status,
            employee.WageType,
            employee.BaseWage,
            employee.CurrencyCode,
            employee.BankName,
            employee.BankAccountNumber,
            employee.EmergencyContactName,
            employee.EmergencyContactPhone,
            employee.CreatedAtUtc);
    }
}