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

    public static bool CanFinalize(string? orderId, string? orderStatus) =>
        !string.IsNullOrWhiteSpace(orderId) &&
        FinalizableOrderStatuses.Contains(orderStatus, StringComparer.OrdinalIgnoreCase);

    public static bool IsCancelled(string? orderStatus) =>
        CancelledOrderStatuses.Contains(orderStatus, StringComparer.OrdinalIgnoreCase);

    public static bool CanReset(HotelCheckoutStatement? statement) =>
        statement != null &&
        (string.IsNullOrWhiteSpace(statement.OrderId) || IsCancelled(statement.Order?.Status));
}
