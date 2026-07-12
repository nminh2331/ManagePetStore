namespace ManagePetStore.Models;

public class HotelFoodOption
{
    public int FoodOptionId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string TargetSpecies { get; set; } = "Tất cả";
    public decimal PricePerDay { get; set; }
    public int DefaultPortionGrams { get; set; }
    public int MealsPerDay { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsIncludedWithPremiumRoom { get; set; }
    public bool Active { get; set; } = true;
    public string? ProductSku { get; set; }
    public virtual Product? Product { get; set; }
    public virtual ICollection<HotelBookingFoodPlan> BookingFoodPlans { get; set; } = [];
}
