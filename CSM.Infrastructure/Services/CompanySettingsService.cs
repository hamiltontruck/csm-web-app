using CSM.Application.Common.Exceptions;
using CSM.Application.Common.Security;
using CSM.Application.CompanySettings;
using CSM.Application.CompanySettings.Dtos;
using CSM.Domain.Constants;
using CSM.Domain.Entities.Organization;
using CSM.Domain.Entities.Identity;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public sealed class CompanySettingsService : ICompanySettingsService
{
    private readonly ApplicationDbContext _context;

    public CompanySettingsService(
        ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<GetCompanySettingsResponse> GetAsync(
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(currentUserId, cancellationToken);

        EnsureAdministrator(user);

        var company = await _context.Companies
            .FirstAsync(
                x => x.Id == user.CompanyId,
                cancellationToken);

        return Map(company);
    }

    public async Task<GetCompanySettingsResponse> UpdateAsync(
        Guid currentUserId,
        UpdateCompanySettingsRequest request,
        CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(currentUserId, cancellationToken);

        EnsureAdministrator(user);

        var company = await _context.Companies
            .FirstAsync(
                x => x.Id == user.CompanyId,
                cancellationToken);

        company.Name = request.Name;
        company.ShortName = request.ShortName;
        company.RegistrationNumber = request.RegistrationNumber;
        company.TaxNumber = request.TaxNumber;
        company.Email = request.Email;
        company.Phone = request.Phone;
        company.Website = request.Website;
        company.Address = request.Address;
        company.City = request.City;
        company.Country = request.Country;
        company.Currency = request.Currency;
        company.TimeZone = request.TimeZone;
        company.FiscalYearStartMonth = request.FiscalYearStartMonth;
        company.WorkStartTime = request.WorkStartTime;
        company.WorkEndTime = request.WorkEndTime;
        company.LunchBreakMinutes = request.LunchBreakMinutes;

        company.LateGraceMinutes = request.LateGraceMinutes;
        company.OvertimeAfterHours = request.OvertimeAfterHours;

        company.NotifyLowInventory = request.NotifyLowInventory;
        company.NotifyMaintenance = request.NotifyMaintenance;
        company.NotifyPermitExpiry = request.NotifyPermitExpiry;
        company.NotifyDailyProgress = request.NotifyDailyProgress;

        company.WeekendDays = request.WeekendDays;

        // Branding
        company.LogoUrl = request.LogoUrl;
        company.PrimaryColor = request.PrimaryColor;
        company.SecondaryColor = request.SecondaryColor;

        // Localization
        company.DefaultLanguage = request.DefaultLanguage;
        company.DateFormat = request.DateFormat;
        company.NumberFormat = request.NumberFormat;
        company.MeasurementUnit = request.MeasurementUnit;

        // Document Numbering
        company.ProjectPrefix = request.ProjectPrefix;
        company.PurchaseOrderPrefix = request.PurchaseOrderPrefix;
        company.StockIssuePrefix = request.StockIssuePrefix;
        company.EmployeePrefix = request.EmployeePrefix;



        await _context.SaveChangesAsync(cancellationToken);



        return Map(company);
    }

    public async Task<IReadOnlyList<CompanyLookupDto>> GetCompaniesAsync(
        CancellationToken cancellationToken)
    {
        return await _context.Companies
            .OrderBy(x => x.Name)
            .Select(x => new CompanyLookupDto
            {
                Id = x.Id,
                Name = x.Name
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<GetCompanySettingsResponse> GetByCompanyIdAsync(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var company = await _context.Companies
            .FirstOrDefaultAsync(
                x => x.Id == companyId,
                cancellationToken);

        if (company is null)
        {
            throw new CompanyManagementException(
                "Company not found.");
        }

        return Map(company);
    }

    private async Task<User> GetUserAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(
                x => x.Id == userId,
                cancellationToken);

        if (user is null)
        {
            throw new CompanyManagementException(
                "Authenticated user was not found.");
        }

        return user;
    }

    private static void EnsureAdministrator(User user)
    {
        if (user.UserRoles.Any(
            x => x.Role.Name == AppRoles.Administrator))
        {
            return;
        }

        throw new CompanyManagementException(
            "Only administrators can modify company settings.");
    }

    private static GetCompanySettingsResponse Map(
        Company company)
    {
        return new GetCompanySettingsResponse
        {
            Id = company.Id,
            Name = company.Name,
            ShortName = company.ShortName ?? string.Empty,
            RegistrationNumber = company.RegistrationNumber ?? string.Empty,
            TaxNumber = company.TaxNumber ?? string.Empty,
            Email = company.Email ?? string.Empty,
            Phone = company.Phone ?? string.Empty,
            Website = company.Website ?? string.Empty,
            Address = company.Address ?? string.Empty,
            City = company.City ?? string.Empty,
            Country = company.Country ?? string.Empty,
            Currency = company.Currency,
            TimeZone = company.TimeZone,
            FiscalYearStartMonth = company.FiscalYearStartMonth,
            WorkStartTime = company.WorkStartTime,
            WorkEndTime = company.WorkEndTime,
            LunchBreakMinutes = company.LunchBreakMinutes,

            LateGraceMinutes = company.LateGraceMinutes,
            OvertimeAfterHours = company.OvertimeAfterHours,

            NotifyLowInventory = company.NotifyLowInventory,
            NotifyMaintenance = company.NotifyMaintenance,
            NotifyPermitExpiry = company.NotifyPermitExpiry,
            NotifyDailyProgress = company.NotifyDailyProgress,

            WeekendDays = company.WeekendDays,

            LogoUrl = company.LogoUrl,
            PrimaryColor = company.PrimaryColor,
            SecondaryColor = company.SecondaryColor,

            DefaultLanguage = company.DefaultLanguage,
            DateFormat = company.DateFormat,
            NumberFormat = company.NumberFormat,
            MeasurementUnit = company.MeasurementUnit,

            ProjectPrefix = company.ProjectPrefix,
            PurchaseOrderPrefix = company.PurchaseOrderPrefix,
            StockIssuePrefix = company.StockIssuePrefix,
            EmployeePrefix = company.EmployeePrefix,
        };
    }
}