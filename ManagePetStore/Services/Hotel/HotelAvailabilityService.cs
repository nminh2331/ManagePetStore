using ManagePetStore.Models;
using Microsoft.EntityFrameworkCore;

namespace ManagePetStore.Services.Hotel;

public sealed class HotelAvailabilityService : IHotelAvailabilityService
{
    private static readonly string[] BlockingStatuses = ["Đã đặt", "Active", "Đang ở"];

    private readonly PetStoreManagementContext _context;

    // [nam] Khởi tạo service truy vấn tình trạng trống của chuồng.
    public HotelAvailabilityService(PetStoreManagementContext context)
    {
        _context = context;
    }

    // [nam] Kiểm tra dữ liệu thời gian và trả về các chuồng có thể đặt.
    public async Task<HotelAvailabilityResult> GetBookableCagesAsync(
        int roomTypeId,
        DateTime checkInDate,
        DateTime checkOutDate)
    {
        bool roomTypeExists = await _context.RoomTypes
            .AsNoTracking()
            .AnyAsync(roomType =>
                roomType.RoomTypeId == roomTypeId &&
                roomType.Status &&
                HotelRoomTypeCatalog.Codes.Contains(roomType.Code));
        if (!roomTypeExists)
        {
            return HotelAvailabilityResult.Fail("Loại phòng không còn nhận đặt.");
        }

        var conflictingCageIds = await _context.HotelBookings
            .AsNoTracking()
            .Where(booking =>
                BlockingStatuses.Contains(booking.Status) &&
                booking.CheckInDate < checkOutDate &&
                (!booking.CheckOutDate.HasValue || booking.CheckOutDate.Value > checkInDate))
            .Select(booking => booking.CageId)
            .Distinct()
            .ToListAsync();

        var cages = await _context.Cages
            .AsNoTracking()
            .Where(cage =>
                cage.RoomTypeId == roomTypeId &&
                cage.Status == "Trống" &&
                !conflictingCageIds.Contains(cage.CageId))
            .OrderBy(cage => cage.CageId)
            .Select(cage => new HotelAvailableCage(cage.CageId, cage.Status))
            .ToListAsync();

        return HotelAvailabilityResult.Ok(cages);
    }

    // [nam] Lấy chuồng đang trống và không ở trạng thái bảo trì hoặc khóa.
    public async Task<IReadOnlyList<HotelAvailableCage>> GetOperationallyEmptyCagesAsync(int roomTypeId)
    {
        return await _context.Cages
            .AsNoTracking()
            .Where(cage =>
                cage.RoomTypeId == roomTypeId &&
                cage.Status == "Trống" &&
                cage.RoomType.Status &&
                HotelRoomTypeCatalog.Codes.Contains(cage.RoomType.Code))
            .OrderBy(cage => cage.CageId)
            .Select(cage => new HotelAvailableCage(cage.CageId, cage.Status))
            .ToListAsync();
    }

    // [nam] Phát hiện lịch lưu trú khác của cùng pet bị giao nhau.
    public Task<bool> HasPetConflictAsync(
        int petId,
        DateTime checkInDate,
        DateTime? checkOutDate,
        int excludedBookingId = 0)
    {
        return _context.HotelBookings.AnyAsync(booking =>
            booking.PetId == petId &&
            booking.HotelBookingId != excludedBookingId &&
            BlockingStatuses.Contains(booking.Status) &&
            (!checkOutDate.HasValue || booking.CheckInDate < checkOutDate.Value) &&
            (!booking.CheckOutDate.HasValue || booking.CheckOutDate.Value > checkInDate));
    }

    // [nam] Phát hiện booking khác đang sử dụng cùng chuồng trong khoảng thời gian yêu cầu.
    public Task<bool> HasCageConflictAsync(
        string cageId,
        DateTime checkInDate,
        DateTime? checkOutDate,
        int excludedBookingId = 0)
    {
        return _context.HotelBookings.AnyAsync(booking =>
            booking.CageId == cageId &&
            booking.HotelBookingId != excludedBookingId &&
            BlockingStatuses.Contains(booking.Status) &&
            (!checkOutDate.HasValue || booking.CheckInDate < checkOutDate.Value) &&
            (!booking.CheckOutDate.HasValue || booking.CheckOutDate.Value > checkInDate));
    }
}
