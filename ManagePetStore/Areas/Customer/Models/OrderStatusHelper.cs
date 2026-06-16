namespace ManagePetStore.Areas.Customer.Models;

public static class OrderStatusHelper
{
    public static string ResolveStatusKey(string? status)
    {
        var normalized = status?.Trim().ToLowerInvariant() ?? "";

        if (normalized.Contains("bị từ chối") ||
            normalized.Contains("bi tu choi") ||
            normalized.Contains("reject"))
        {
            return "rejected";
        }

        if (normalized.Contains("hủy") ||
            normalized.Contains("huy") ||
            normalized.Contains("cancel"))
        {
            return "cancelled";
        }

        if (normalized == "approved" ||
            normalized.Contains("đã phê duyệt") ||
            normalized.Contains("da phe duyet"))
        {
            return "approved";
        }

        if (normalized.Contains("hoàn thành") ||
            normalized.Contains("hoan thanh") ||
            normalized == "completed")
        {
            return "completed";
        }

        if (normalized.Contains("đang giao") ||
            normalized.Contains("đang phân phối") ||
            normalized.Contains("dang phan phoi") ||
            normalized == "delivering" ||
            (normalized.Contains("giao") && !normalized.Contains("hoàn")))
        {
            return "delivering";
        }

        if (normalized.Contains("chờ") ||
            normalized.Contains("cho") ||
            normalized.Contains("pending") ||
            normalized.Contains("xử lý") ||
            normalized.Contains("xu ly"))
        {
            return "pending";
        }

        return "pending";
    }

    public static string FormatStatusLabel(string statusKey, string? originalStatus)
    {
        return statusKey switch
        {
            "pending" => "ĐANG CHỜ XỬ LÝ",
            "approved" => "ĐÃ PHÊ DUYỆT",
            "delivering" => "ĐANG GIAO HÀNG",
            "completed" => "ĐÃ HOÀN THÀNH",
            "rejected" => "BỊ TỪ CHỐI",
            "cancelled" => "ĐÃ HỦY",
            _ => string.IsNullOrWhiteSpace(originalStatus) ? "ĐANG CHỜ XỬ LÝ" : originalStatus.ToUpperInvariant()
        };
    }
}
