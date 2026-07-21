using Microsoft.AspNetCore.Http;

namespace ManagePetStore.Services;

public interface IHotelCareMediaService
{
    // [nam] Kiểm tra và lưu media nhật ký chăm sóc của booking.
    Task<HotelCareMediaResult?> SaveAsync(int hotelBookingId, IFormFile? file);

    // [nam] Xoá media nhật ký chăm sóc theo URL công khai.
    Task DeleteAsync(string? publicUrl);
}

public record HotelCareMediaResult(string PublicUrl, string MediaType);
