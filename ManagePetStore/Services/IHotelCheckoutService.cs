using ManagePetStore.Areas.ServiceStaff.Models;

namespace ManagePetStore.Services;

public interface IHotelCheckoutService
{
    // [nam] Lấy bảng tính checkout tạm thời của booking.
    Task<HotelCheckoutPreviewViewModel?> GetPreviewAsync(int bookingId, DateTime? checkoutAt = null);

    // [nam] Lập và lưu bảng kê checkout để chuyển sang quầy thu ngân.
    Task<HotelCheckoutPreviewViewModel> PrepareAsync(PrepareHotelCheckoutRequest request, int? staffUserId, string staffName);
}
