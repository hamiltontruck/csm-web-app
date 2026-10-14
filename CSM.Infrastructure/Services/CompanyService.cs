using CSM.Application.Common.Exceptions;
using CSM.Application.Companies;
using CSM.Application.Companies.Dtos;
using CSM.Domain.Entities.Organization;
using CSM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSM.Infrastructure.Services;

public sealed class CompanyService : ICompanyService
{
    private readonly ApplicationDbContext _dbContext;

    public CompanyService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<CompanyResponse>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var companies = await _dbContext.Companies
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return companies
            .Select(Map)
            .ToArray();
    }

    public async Task<CompanyResponse> GetByIdAsync(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var company = await GetCompanyAsync(
            companyId,
            cancellationToken);

        return Map(company);
    }

    public async Task<CompanyResponse> CreateAsync(
        CreateCompanyRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(
            request.Code,
            request.Name,
            request.BaseCurrencyCode);

        var normalizedCode =
            request.Code.Trim().ToUpperInvariant();

        var codeExists = await _dbContext.Companies
            .IgnoreQueryFilters()
            .AnyAsync(
                x => x.Code.ToUpper() == normalizedCode,
                cancellationToken);

        if (codeExists)
        {
            throw new CompanyManagementException(
                "A company with this code already exists.");
        }

        var company = new Company
        {
            Code = normalizedCode,
            Name = request.Name.Trim(),
            LegalName = Clean(request.LegalName),
            TaxIdentificationNumber =
                Clean(request.TaxIdentificationNumber),
            RegistrationNumber =
                Clean(request.RegistrationNumber),
            PhoneNumber = Clean(request.PhoneNumber),
            Email = Clean(request.Email),
            Address = Clean(request.Address),
            City = Clean(request.City),
            Country = Clean(request.Country),

            BaseCurrencyCode =
                request.BaseCurrencyCode
                    .Trim()
                    .ToUpperInvariant(),

            IsActive = true
        };

        _dbContext.Companies.Add(company);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(company);
    }

    public async Task<CompanyResponse> UpdateAsync(
        Guid companyId,
        UpdateCompanyRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(
            "UNCHANGED",
            request.Name,
            request.BaseCurrencyCode);

        var company = await GetCompanyAsync(
            companyId,
            cancellationToken);

        company.Name = request.Name.Trim();
        company.LegalName = Clean(request.LegalName);
        company.TaxIdentificationNumber =
            Clean(request.TaxIdentificationNumber);
        company.RegistrationNumber =
            Clean(request.RegistrationNumber);
        company.PhoneNumber = Clean(request.PhoneNumber);
        company.Email = Clean(request.Email);
        company.Address = Clean(request.Address);
        company.City = Clean(request.City);
        company.Country = Clean(request.Country);

        company.BaseCurrencyCode =
            request.BaseCurrencyCode
                .Trim()
                .ToUpperInvariant();

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return Map(company);
    }

    public async Task SetActiveStatusAsync(
        Guid companyId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var company = await GetCompanyAsync(
            companyId,
            cancellationToken);

        company.IsActive = isActive;

        await _dbContext.SaveChangesAsync(
            cancellationToken);
    }

    private async Task<Company> GetCompanyAsync(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Companies
            .SingleOrDefaultAsync(
                x => x.Id == companyId,
                cancellationToken)
            ?? throw new CompanyManagementException(
                "Company was not found.");
    }

    private static void Validate(
        string code,
        string name,
        string currencyCode)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new CompanyManagementException(
                "Company code is required.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new CompanyManagementException(
                "Company name is required.");
        }

        if (string.IsNullOrWhiteSpace(currencyCode))
        {
            throw new CompanyManagementException(
                "Base currency code is required.");
        }

        if (currencyCode.Trim().Length != 3)
        {
            throw new CompanyManagementException(
                "Base currency code must contain exactly 3 characters.");
        }
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static CompanyResponse Map(Company company)
    {
        return new CompanyResponse(
            company.Id,
            company.Code,
            company.Name,
            company.LegalName,
            company.TaxIdentificationNumber,
            company.RegistrationNumber,
            company.PhoneNumber,
            company.Email,
            company.Address,
            company.City,
            company.Country,
            company.BaseCurrencyCode,
            company.IsActive,
            company.CreatedAtUtc);
    }
}