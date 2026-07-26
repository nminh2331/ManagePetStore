using ManagePetStore.Areas.ServiceStaff.Models;

namespace ManagePetStore.Services.Hotel;

public interface IHotelReceptionService
{
    // [nam] Kiểm tra sức khỏe và tiếp nhận pet vào chuồng.
    Task<HotelCommandResult> CheckInAsync(
        HotelCheckInRequest request,
        int? staffUserId,
        string staffName);

    // [nam] Ghi nhận từ chối tiếp nhận và hoàn lại tài nguyên đã giữ.
    Task<HotelCommandResult> RejectAsync(
        HotelCheckInRequest request,
        int? staffUserId,
        string staffName);
}
