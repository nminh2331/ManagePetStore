namespace ManagePetStore.Services;

public interface IHotelEmailService
{
    // [nam] Gửi xác nhận booking mới cho khách hàng.
    Task SendBookingCreatedAsync(string? email, string customerName, int bookingId, string petName,
        string cageId, string roomTypeName, DateTime checkIn, DateTime checkOut, decimal totalAmount);

    // [nam] Gửi thông báo check-in cho khách hàng.
    Task SendCheckInAsync(string? email, string customerName, int bookingId, string petName,
        string cageId, DateTime checkIn, DateTime? expectedCheckOut);

    // [nam] Gửi thông báo check-out cho khách hàng.
    Task SendCheckOutAsync(string? email, string customerName, int bookingId, string petName,
        string cageId, DateTime checkOut, decimal totalAmount);

    // [nam] Gửi cập nhật nhật ký chăm sóc cho khách hàng.
    Task SendCareLogAsync(string? email, string customerName, int bookingId, string petName,
        string title, string message, DateTime occurredAt);

    // [nam] Gửi kết quả xử lý yêu cầu đổi chuồng.
    Task SendCageChangeDecisionAsync(string? email, string customerName, int bookingId, string petName,
        string sourceCageId, string targetCageId, bool approved, decimal priceDifference, string? note);
}
