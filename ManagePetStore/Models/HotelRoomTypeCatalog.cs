namespace ManagePetStore.Models;

public static class HotelRoomTypeCatalog
{
    public const string StandardCode = "STANDARD";
    public const string VipCode = "VIP";
    public const string LuxuryCode = "LUXURY";

    public static readonly string[] Codes = [StandardCode, VipCode, LuxuryCode];

    private static readonly HotelRoomServiceProfile StandardProfile = new(
        StandardCode,
        "Standard",
        2,
        1,
        ["Vệ sinh hằng ngày", "Kiểm tra 2 lần/ngày", "Nhật ký 1 lần/ngày"]);

    private static readonly HotelRoomServiceProfile VipProfile = new(
        VipCode,
        "VIP",
        3,
        2,
        ["Kiểm tra 3 lần/ngày", "2 ảnh/video mỗi ngày", "Vận động riêng"]);

    private static readonly HotelRoomServiceProfile LuxuryProfile = new(
        LuxuryCode,
        "Luxury",
        4,
        3,
        ["Kiểm tra 4 lần/ngày", "3 cập nhật mỗi ngày", "Vận động và enrichment", "Ưu tiên hỗ trợ", "Chải lông nhẹ trước khi trả"]);

    public static readonly string[] CoreServices =
    [
        "Nước sạch và vệ sinh hằng ngày",
        "Cho ăn đúng gói đã chọn",
        "Theo dõi sức khỏe và xử lý bất thường"
    ];

    public static bool IsSupported(string? code) =>
        !string.IsNullOrWhiteSpace(code) &&
        Codes.Contains(code.Trim().ToUpperInvariant(), StringComparer.Ordinal);

    public static HotelRoomServiceProfile GetServiceProfile(string? code) =>
        code?.Trim().ToUpperInvariant() switch
        {
            VipCode => VipProfile,
            LuxuryCode => LuxuryProfile,
            _ => StandardProfile
        };
}

public sealed record HotelRoomServiceProfile(
    string Code,
    string DisplayName,
    int CareChecksPerDay,
    int CustomerUpdatesPerDay,
    IReadOnlyList<string> IncludedServices)
{
    public string Summary => string.Join(" · ", IncludedServices);
}
