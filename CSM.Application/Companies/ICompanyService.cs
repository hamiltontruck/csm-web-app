using CSM.Application.Companies.Dtos;

namespace CSM.Application.Companies;

public interface ICompanyService
{
    Task<IReadOnlyCollection<CompanyResponse>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<CompanyResponse> GetByIdAsync(
        Guid companyId,
        CancellationToken cancellationToken = default);

    Task<CompanyResponse> CreateAsync(
        CreateCompanyRequest request,
        CancellationToken cancellationToken = default);

    Task<CompanyResponse> UpdateAsync(
        Guid companyId,
        UpdateCompanyRequest request,
        CancellationToken cancellationToken = default);

    Task SetActiveStatusAsync(
        Guid companyId,
        bool isActive,
        CancellationToken cancellationToken = default);
}