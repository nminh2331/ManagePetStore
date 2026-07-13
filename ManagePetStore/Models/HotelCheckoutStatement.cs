namespace ManagePetStore.Models;

public class HotelCheckoutStatement
{
    public int CheckoutStatementId { get; set; }
    public int HotelBookingId { get; set; }
    public string Status { get; set; } = "ReadyForPayment";
    public DateTime CheckoutAt { get; set; }
    public decimal RoomAmount { get; set; }
    public decimal FoodAmount { get; set; }
    public decimal AddonAmount { get; set; }
    public decimal LateFeeAmount { get; set; }
    public decimal OtherAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public int? PreparedByUserId { get; set; }
    public string PreparedByName { get; set; } = null!;
    public DateTime PreparedAt { get; set; }
    public string? OrderId { get; set; }
    public DateTime? PaidAt { get; set; }
    public virtual HotelBooking HotelBooking { get; set; } = null!;
    public virtual Order? Order { get; set; }
    public virtual ICollection<HotelCheckoutItem> Items { get; set; } = [];
}
