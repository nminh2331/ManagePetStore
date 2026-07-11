namespace ManagePetStore.Models;

/// <summary>
/// Ghi nhận lịch sử tìm kiếm và đếm lượt tìm theo keyword.
/// Dùng để boost kết quả tìm kiếm theo độ phổ biến.
/// </summary>
public class SearchLog
{
    public int LogId { get; set; }

    /// <summary>Keyword người dùng đã nhập (đã normalize: lowercase, bỏ dấu)</summary>
    public string Keyword { get; set; } = null!;

    /// <summary>Số lần keyword này được tìm kiếm</summary>
    public int SearchCount { get; set; } = 1;

    /// <summary>Thời điểm tìm kiếm gần nhất</summary>
    public DateTime LastSearchedAt { get; set; } = DateTime.Now;
}
