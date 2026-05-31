using System.ComponentModel.DataAnnotations;

namespace ManagePetStore.Models;

public class RoomViewModel
{
    public int RoomId { get; set; }

    [Required(ErrorMessage = "Mã chuồng không được để trống")]
    [Display(Name = "Mã chuồng")]
    [StringLength(50, ErrorMessage = "Mã chuồng tối đa 50 ký tự")]
    public string RoomCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Loại chuồng không được để trống")]
    [Display(Name = "Loại chuồng")]
    public string RoomType { get; set; } = string.Empty;

    [Required(ErrorMessage = "Giá theo ngày không được để trống")]
    [Display(Name = "Giá/ngày (đ)")]
    [Range(1000, double.MaxValue, ErrorMessage = "Giá tối thiểu 1.000đ")]
    public decimal DailyRate { get; set; }

    [Display(Name = "Trạng thái")]
    public string Status { get; set; } = ManagePetStore.Models.RoomStatus.Available;

    [Display(Name = "Kích thước")]
    [StringLength(100, ErrorMessage = "Kích thước tối đa 100 ký tự")]
    public string? Dimensions { get; set; }
}
