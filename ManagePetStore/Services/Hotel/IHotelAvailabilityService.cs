namespace ManagePetStore.Services.Hotel;

public interface IHotelAvailabilityService
{
    // [nam] Lấy các chuồng có thể đặt trong khoảng thời gian yêu cầu.
    Task<HotelAvailabilityResult> GetBookableCagesAsync(
        int roomTypeId,
        DateTime checkInDate,
        DateTime checkOutDate);

    // [nam] Lấy các chuồng đang trống về mặt vận hành theo loại chuồng.
    Task<IReadOnlyList<HotelAvailableCage>> GetOperationallyEmptyCagesAsync(int roomTypeId);

    // [nam] Kiểm tra pet có booking khác bị trùng lịch hay không.
    Task<bool> HasPetConflictAsync(
        int petId,
        DateTime checkInDate,
        DateTime? checkOutDate,
        int excludedBookingId = 0);

    // [nam] Kiểm tra chuồng có booking khác chiếm dụng trong khoảng thời gian hay không.
    Task<bool> HasCageConflictAsync(
        string cageId,
        DateTime checkInDate,
        DateTime? checkOutDate,
        int excludedBookingId = 0);
}
