using ManagePetStore.Models;

namespace ManagePetStore.Services;

public interface IHotelBookingHistoryService
{
    Task<HotelBookingHistoryDetailViewModel?> GetDetailAsync(
        int hotelBookingId,
        int? customerId = null);
}
