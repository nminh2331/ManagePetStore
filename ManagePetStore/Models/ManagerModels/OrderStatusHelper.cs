namespace ManagePetStore.Models.ManagerModels;

public static class OrderStatusHelper
{
    public const string Pending = "Chờ xử lý";
    public const string Approved = "Đã phê duyệt";
    public const string Delivering = "Đang giao hàng";
    public const string Completed = "Đã hoàn thành";
    public const string Rejected = "Bị từ chối";
    public const string Cancelled = "Đã hủy";

    public static string Normalize(string? status)
    {
        return status?.Trim().ToLowerInvariant() ?? "";
    }

    public static bool IsPending(string? status)
    {
        var normalized = Normalize(status);
        return normalized.Contains("chờ") ||
               normalized.Contains("cho") ||
               normalized.Contains("pending") ||
               normalized.Contains("xử lý") ||
               normalized.Contains("xu ly") ||
               normalized == "đã thanh toán" ||
               normalized == "da thanh toan";
    }

    public static bool IsApproved(string? status)
    {
        var normalized = Normalize(status);
        return normalized == "approved" ||
               normalized.Contains("đã phê duyệt") ||
               normalized.Contains("da phe duyet");
    }

    public static bool IsDelivering(string? status)
    {
        var normalized = Normalize(status);
        return normalized == "delivering" ||
               normalized.Contains("đang phân phối") ||
               normalized.Contains("dang phan phoi") ||
               normalized.Contains("đang giao hàng") ||
               normalized.Contains("dang giao hang") ||
               normalized.Contains("đang giao") ||
               (normalized.Contains("giao") && !normalized.Contains("hoàn"));
    }

    public static bool IsCompleted(string? status)
    {
        var normalized = Normalize(status);
        return normalized == "completed" ||
               normalized.Contains("hoàn thành") ||
               normalized.Contains("hoan thanh");
    }

    public static bool IsCancelled(string? status)
    {
        var normalized = Normalize(status);
        return normalized.Contains("hủy") ||
               normalized.Contains("huy") ||
               normalized.Contains("cancel");
    }

    public static bool IsRejected(string? status)
    {
        var normalized = Normalize(status);
        return normalized.Contains("bị từ chối") ||
               normalized.Contains("bi tu choi") ||
               normalized.Contains("reject");
    }

    public static string ResolveStatusKey(string? status)
    {
        if (IsRejected(status))
        {
            return "rejected";
        }

        if (IsCancelled(status))
        {
            return "cancelled";
        }

        if (IsApproved(status))
        {
            return "approved";
        }

        if (IsDelivering(status))
        {
            return "delivering";
        }

        if (IsCompleted(status))
        {
            return "completed";
        }

        return "pending";
    }

    public static string DisplayLabel(string? status)
    {
        if (IsRejected(status))
        {
            return "BỊ TỪ CHỐI";
        }

        if (IsCancelled(status))
        {
            return "ĐÃ HỦY";
        }

        if (IsApproved(status))
        {
            return "ĐÃ PHÊ DUYỆT";
        }

        if (IsDelivering(status))
        {
            return "ĐANG GIAO HÀNG";
        }

        if (IsCompleted(status))
        {
            return "ĐÃ HOÀN THÀNH";
        }

        if (IsPending(status))
        {
            return "ĐANG CHỜ XỬ LÝ";
        }

        return string.IsNullOrWhiteSpace(status) ? "ĐANG CHỜ XỬ LÝ" : status.ToUpperInvariant();
    }
}

