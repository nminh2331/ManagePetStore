namespace ManagePetStore.Models;

public partial class HotelBooking
{
    public virtual HotelBookingFoodPlan? FoodPlan { get; set; }
    public virtual HotelCheckoutStatement? CheckoutStatement { get; set; }
    public virtual ICollection<HotelStaySpaLink> SpaLinks { get; set; } = [];
}
