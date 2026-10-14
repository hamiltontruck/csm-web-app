using CSM.Web.Models.HRM;

namespace CSM.Web.Services;

public interface IContractorService
{
    Task<IReadOnlyList<ContractorResponse>> GetContractorsAsync(
        bool? isActive = null);

    Task<ContractorResponse> GetContractorAsync(
        Guid contractorId);

    Task<ContractorResponse> CreateContractorAsync(
        CreateContractorRequest request);

    Task<ContractorResponse> UpdateContractorAsync(
        Guid contractorId,
        UpdateContractorRequest request);
}