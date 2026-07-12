namespace ManagePetStore.Models;

public class HotelCheckoutItem
{
    public int CheckoutItemId { get; set; }
    public int CheckoutStatementId { get; set; }
    public string ChargeType { get; set; } = null!;
    public string Description { get; set; } = null!;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = null!;
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }
    public string? SourceType { get; set; }
    public string? SourceId { get; set; }
    public virtual HotelCheckoutStatement CheckoutStatement { get; set; } = null!;
}
