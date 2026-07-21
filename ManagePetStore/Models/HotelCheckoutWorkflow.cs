namespace ManagePetStore.Models;

public static class HotelCheckoutWorkflow
{
    private static readonly string[] FinalizableOrderStatuses =
    [
        "Chờ xử lý",
        "Đã thanh toán",
        "PAID",
        "Success",
        "Completed"
    ];

    private static readonly string[] CancelledOrderStatuses =
    [
        "Đã hủy",
        "Cancelled",
        "Canceled"
    ];

    // [nam] Xác định bảng kê đã có đơn hàng ở trạng thái cho phép hoàn tất hay chưa.
    public static bool CanFinalize(string? orderId, string? orderStatus) =>
        !string.IsNullOrWhiteSpace(orderId) &&
        FinalizableOrderStatuses.Contains(orderStatus, StringComparer.OrdinalIgnoreCase);

    // [nam] Kiểm tra đơn thanh toán đã bị huỷ theo các trạng thái hỗ trợ.
    public static bool IsCancelled(string? orderStatus) =>
        CancelledOrderStatuses.Contains(orderStatus, StringComparer.OrdinalIgnoreCase);

    // [nam] Chỉ cho phép lập lại checkout khi chưa có đơn hoặc đơn cũ đã huỷ.
    public static bool CanReset(HotelCheckoutStatement? statement) =>
        statement != null &&
        (string.IsNullOrWhiteSpace(statement.OrderId) || IsCancelled(statement.Order?.Status));
}
