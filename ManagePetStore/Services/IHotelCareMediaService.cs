using Microsoft.AspNetCore.Http;

namespace ManagePetStore.Services;

public interface IHotelCareMediaService
{
    Task<HotelCareMediaResult?> SaveAsync(int hotelBookingId, IFormFile? file);

    Task DeleteAsync(string? publicUrl);
}

public record HotelCareMediaResult(string PublicUrl, string MediaType);
