using System.Net;

namespace ManagePetStore.Services;

public class HotelEmailService : IHotelEmailService
{
    private readonly IEmailService _emailService;
    private readonly ILogger<HotelEmailService> _logger;

    // [nam] Khởi tạo dịch vụ gửi email cho các sự kiện lưu trú.
    public HotelEmailService(IEmailService emailService, ILogger<HotelEmailService> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    // [nam] Gửi email xác nhận khi khách hàng đặt chuồng thành công.
    public Task SendBookingCreatedAsync(string? email, string customerName, int bookingId, string petName,
        string cageId, string roomTypeName, DateTime checkIn, DateTime checkOut, decimal totalAmount) =>
        TrySendAsync(email, $"Đặt chuồng HB{bookingId:0000} thành công", $"""
            <h2>Đặt chuồng lưu trú thành công</h2>
            <p>Xin chào {E(customerName)},</p>
            <p>Pet <strong>{E(petName)}</strong> đã được giữ chuồng <strong>{E(cageId)}</strong> ({E(roomTypeName)}).</p>
            <p>Nhận: <strong>{checkIn:dd/MM/yyyy HH:mm}</strong><br>
            Trả dự kiến: <strong>{checkOut:dd/MM/yyyy HH:mm}</strong><br>
            Tạm tính: <strong>{totalAmount:N0}đ</strong></p>
            <p>Mã booking: <strong>HB{bookingId:0000}</strong>.</p>
            """, bookingId, "booking");

    // [nam] Gửi email thông báo pet đã được tiếp nhận vào chuồng.
    public Task SendCheckInAsync(string? email, string customerName, int bookingId, string petName,
        string cageId, DateTime checkIn, DateTime? expectedCheckOut) =>
        TrySendAsync(email, $"Đã tiếp nhận {petName} vào chuồng", $"""
            <h2>Đã tiếp nhận thú cưng</h2>
            <p>Xin chào {E(customerName)},</p>
            <p>Pet <strong>{E(petName)}</strong> đã được tiếp nhận vào chuồng <strong>{E(cageId)}</strong> lúc {checkIn:dd/MM/yyyy HH:mm}.</p>
            <p>Trả dự kiến: <strong>{(expectedCheckOut.HasValue ? expectedCheckOut.Value.ToString("dd/MM/yyyy HH:mm") : "Chưa xác định")}</strong>.</p>
            <p>Mã booking: <strong>HB{bookingId:0000}</strong>.</p>
            """, bookingId, "check-in");

    // [nam] Gửi email xác nhận hoàn tất trả pet và tổng chi phí.
    public Task SendCheckOutAsync(string? email, string customerName, int bookingId, string petName,
        string cageId, DateTime checkOut, decimal totalAmount) =>
        TrySendAsync(email, $"Đã hoàn tất lưu trú HB{bookingId:0000}", $"""
            <h2>Đã hoàn tất trả thú cưng</h2>
            <p>Xin chào {E(customerName)},</p>
            <p>Pet <strong>{E(petName)}</strong> đã hoàn tất lưu trú tại chuồng <strong>{E(cageId)}</strong> lúc {checkOut:dd/MM/yyyy HH:mm}.</p>
            <p>Tổng tiền đã chốt: <strong>{totalAmount:N0}đ</strong>.</p>
            """, bookingId, "check-out");

    // [nam] Gửi email cập nhật nhật ký chăm sóc mới cho chủ pet.
    public Task SendCareLogAsync(string? email, string customerName, int bookingId, string petName,
        string title, string message, DateTime occurredAt) =>
        TrySendAsync(email, $"Nhật ký chăm sóc của {petName}", $"""
            <h2>{E(title)}</h2>
            <p>Xin chào {E(customerName)},</p>
            <p>Cập nhật của <strong>{E(petName)}</strong> lúc {occurredAt:dd/MM/yyyy HH:mm}:</p>
            <p>{E(message)}</p>
            <p>Mã booking: <strong>HB{bookingId:0000}</strong>.</p>
            """, bookingId, "care log");

    // [nam] Gửi kết quả duyệt hoặc từ chối yêu cầu đổi chuồng.
    public Task SendCageChangeDecisionAsync(string? email, string customerName, int bookingId, string petName,
        string sourceCageId, string targetCageId, bool approved, decimal priceDifference, string? note)
    {
        var result = approved ? "đã được duyệt" : "đã bị từ chối";
        var priceText = approved
            ? priceDifference > 0
                ? $"Phụ thu dự kiến: <strong>{priceDifference:N0}đ</strong>."
                : priceDifference < 0
                    ? $"Giảm trừ dự kiến: <strong>{Math.Abs(priceDifference):N0}đ</strong>."
                    : "Không phát sinh chênh lệch giá."
            : string.Empty;
        return TrySendAsync(email, $"Yêu cầu đổi chuồng HB{bookingId:0000} {result}", $"""
            <h2>Yêu cầu đổi chuồng {result}</h2>
            <p>Xin chào {E(customerName)},</p>
            <p>Yêu cầu đổi chuồng của <strong>{E(petName)}</strong> từ <strong>{E(sourceCageId)}</strong> sang <strong>{E(targetCageId)}</strong> {result}.</p>
            <p>{priceText}</p>
            <p>Ghi chú: {E(string.IsNullOrWhiteSpace(note) ? "Không có" : note)}</p>
            """, bookingId, "cage change");
    }

    // [nam] Gửi email theo cơ chế best-effort và ghi log nếu nhà cung cấp email gặp lỗi.
    private async Task TrySendAsync(string? email, string subject, string body, int bookingId, string eventName)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        try
        {
            await _emailService.SendEmailAsync(email.Trim(), subject, body);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Hotel {EventName} was saved but email delivery failed for booking {BookingId}.",
                eventName,
                bookingId);
        }
    }

    // [nam] Mã hoá dữ liệu động trước khi chèn vào nội dung HTML của email.
    private static string E(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
}
