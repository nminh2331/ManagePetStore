using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace ManagePetStore.Areas.ServiceStaff.Models;

public class HotelCareLogRequest
{
    [Required]
    public int HotelBookingId { get; set; }

    [Required, StringLength(30)]
    public string ActivityType { get; set; } = "General";

    [Required, StringLength(150)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(30)]
    public string Status { get; set; } = string.Empty;

    [StringLength(100)]
    public string? FoodType { get; set; }

    [StringLength(50)]
    public string? Amount { get; set; }

    [StringLength(1000)]
    public string? Note { get; set; }

    public DateTime? OccurredAt { get; set; }

    public bool IsVisibleToCustomer { get; set; } = true;

    public IFormFile? MediaFile { get; set; }

    public string? MealType { get; set; }
    [Range(0, 10000)] public decimal? ServedGrams { get; set; }
    [Range(0, 100)] public int? ConsumedPercent { get; set; }
    public bool IsExtraCharge { get; set; }
    [Range(0, 100000000)] public decimal ExtraChargeAmount { get; set; }
    public bool ReturnToPetDaily { get; set; }
}
