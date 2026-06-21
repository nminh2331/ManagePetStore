using System.ComponentModel.DataAnnotations;

namespace ManagePetStore.Models;

public class RoomViewModel
{
    public int RoomId { get; set; }

    [Required(ErrorMessage = "M� chu?ng kh�ng du?c d? tr?ng")]
    [Display(Name = "M� chu?ng")]
    [StringLength(50, ErrorMessage = "M� chu?ng t?i da 50 k� t?")]
    public string RoomCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Lo?i chu?ng kh�ng du?c d? tr?ng")]
    [Display(Name = "Lo?i chu?ng")]
    public string RoomType { get; set; } = string.Empty;

    [Required(ErrorMessage = "Gi� theo ng�y kh�ng du?c d? tr?ng")]
    [Display(Name = "Gi�/ng�y (d)")]
    [Range(1000, double.MaxValue, ErrorMessage = "Gi� t?i thi?u 1.000d")]
    public decimal DailyRate { get; set; }

    [Display(Name = "Tr?ng th�i")]
    public string Status { get; set; } = ManagePetStore.Models.RoomStatus.Available;

    [Display(Name = "K�ch thu?c")]
    [StringLength(100, ErrorMessage = "K�ch thu?c t?i da 100 k� t?")]
    public string? Dimensions { get; set; }
}
