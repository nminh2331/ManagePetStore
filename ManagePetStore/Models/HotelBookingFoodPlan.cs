namespace ManagePetStore.Models;

public class HotelBookingFoodPlan
{
    public int FoodPlanId { get; set; }
    public int HotelBookingId { get; set; }
    public int? FoodOptionId { get; set; }
    public string? ProductSku { get; set; }
    public string PlanType { get; set; } = "OwnerProvided";
    public string FoodNameSnapshot { get; set; } = "Chủ nuôi tự chuẩn bị";
    public string ProductUnitSnapshot { get; set; } = HotelFoodCatalog.DailyUnit;
    public decimal PricePerDaySnapshot { get; set; }
    public int PortionGrams { get; set; }
    public int MealsPerDay { get; set; }
    public string? FeedingInstructions { get; set; }
    public string? AllergyNotes { get; set; }
    public int ChargeableDays { get; set; }
    public int InventoryQuantityDeducted { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public virtual HotelBooking HotelBooking { get; set; } = null!;
    public virtual HotelFoodOption? FoodOption { get; set; }
    public virtual Product? Product { get; set; }
    public virtual ICollection<FoodDiaryLog> FoodDiaryLogs { get; set; } = [];
}
