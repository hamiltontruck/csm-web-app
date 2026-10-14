using CSM.Application.Procurement.Vendors.Dtos;

namespace CSM.Application.Procurement.Vendors;

public interface IVendorService
{
    Task<IReadOnlyCollection<VendorResponse>> GetAllAsync(
        Guid currentUserId,
        bool? isActive,
        CancellationToken cancellationToken = default);

    Task<VendorResponse> GetByIdAsync(
        Guid currentUserId,
        Guid vendorId,
        CancellationToken cancellationToken = default);

    Task<VendorResponse> CreateAsync(
        Guid currentUserId,
        CreateVendorRequest request,
        CancellationToken cancellationToken = default);

    Task<VendorResponse> UpdateAsync(
        Guid currentUserId,
        Guid vendorId,
        UpdateVendorRequest request,
        CancellationToken cancellationToken = default);
}
