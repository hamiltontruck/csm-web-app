using CSM.Web.Models.Procurement;

namespace CSM.Web.Services;

public interface IVendorService
{
    Task<List<VendorResponse>> GetVendorsAsync();

    Task<VendorResponse?> GetVendorAsync(Guid id);

    Task<bool> CreateVendorAsync(CreateVendorRequest request);

    Task<bool> UpdateVendorAsync(Guid id, CreateVendorRequest request);

    Task<bool> DeleteVendorAsync(Guid id);
}