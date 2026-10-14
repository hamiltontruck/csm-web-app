using CSM.Application.CompanySettings.Dtos;

namespace CSM.Application.CompanySettings;

public interface ICompanySettingsService
{
    Task<GetCompanySettingsResponse> GetAsync(
        Guid currentUserId,
        CancellationToken cancellationToken);

    Task<GetCompanySettingsResponse> GetByCompanyIdAsync(
        Guid companyId,
        CancellationToken cancellationToken);

    Task<GetCompanySettingsResponse> UpdateAsync(
        Guid currentUserId,
        UpdateCompanySettingsRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CompanyLookupDto>> GetCompaniesAsync(
        CancellationToken cancellationToken);


}