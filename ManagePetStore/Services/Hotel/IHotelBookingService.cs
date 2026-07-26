using ManagePetStore.Areas.Customer.Models;

namespace ManagePetStore.Services.Hotel;

public interface IHotelBookingService
{
    // [nam] Tạo booking chuồng mới cho khách hàng.
    Task<HotelCommandResult> CreateAsync(HotelBookingRequest request, int customerId);

    // [nam] Hủy booking chuồng và hoàn lại phần thức ăn đã giữ nếu hợp lệ.
    Task<HotelCommandResult> CancelAsync(int bookingId, int customerId);
}
