using ManagePetStore.Areas.ServiceStaff.Models;

namespace ManagePetStore.Services;

public interface IHotelCheckoutService
{
    Task<HotelCheckoutPreviewViewModel?> GetPreviewAsync(int bookingId, DateTime? checkoutAt = null);
    Task<HotelCheckoutPreviewViewModel> PrepareAsync(PrepareHotelCheckoutRequest request, int? staffUserId, string staffName);
}
