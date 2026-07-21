using System.Text.RegularExpressions;

namespace ManagePetStore.Models;

public static partial class CageTerminology
{
    // [nam] Chuẩn hoá thuật ngữ Hotel cũ sang cách hiển thị theo mô hình chuồng lưu trú.
    public static string ForDisplay(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value ?? string.Empty;
        }

        var normalized = value
            .Replace("Pet Hotel", "lưu trú chuồng", StringComparison.OrdinalIgnoreCase)
            .Replace("Đặt phòng Hotel", "Đặt chuồng", StringComparison.OrdinalIgnoreCase)
            .Replace("booking Hotel", "lượt đặt chuồng", StringComparison.OrdinalIgnoreCase);

        return HotelWordRegex().Replace(normalized, match =>
            string.Equals(match.Value, match.Value.ToUpperInvariant(), StringComparison.Ordinal)
                ? "CAGE"
                : "chuồng");
    }

    [GeneratedRegex("hotel", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    // [nam] Tạo biểu thức chính quy biên dịch sẵn để tìm thuật ngữ Hotel.
    private static partial Regex HotelWordRegex();
}
