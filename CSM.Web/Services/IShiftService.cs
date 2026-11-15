using CSM.Web.Models.HRM;

namespace CSM.Web.Services;

public interface IShiftService
{
    Task<CSM.Web.Models.Common.CompanySelectionOptions> GetShiftCompanyOptionsAsync();
    Task<IReadOnlyCollection<ShiftResponse>> GetAllAsync();

    Task<ShiftResponse?> GetByIdAsync(Guid shiftId);

    Task<ShiftResponse> CreateAsync(
        CreateShiftRequest request);

    Task<ShiftResponse> UpdateAsync(
        Guid shiftId,
        UpdateShiftRequest request);

    Task<ShiftResponse> ChangeActiveStatusAsync(
        Guid shiftId,
        ChangeShiftActiveStatusRequest request);
}