using ManagePetStore.Models;

namespace ManagePetStore.Services;

public interface IHotelBookingHistoryService
{
    // [nam] Lấy chi tiết lịch sử lưu trú theo booking và phạm vi khách hàng.
    Task<HotelBookingHistoryDetailViewModel?> GetDetailAsync(
        int hotelBookingId,
        int? customerId = null);
}
