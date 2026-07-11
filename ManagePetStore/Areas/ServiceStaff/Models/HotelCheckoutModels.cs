using System.ComponentModel.DataAnnotations;

namespace ManagePetStore.Areas.ServiceStaff.Models;

public class PrepareHotelCheckoutRequest
{
    [Required] public int HotelBookingId { get; set; }
    [StringLength(250)] public string? OtherDescription { get; set; }
    [Range(0, 100000000)] public decimal OtherAmount { get; set; }
}

public class HotelCheckoutPreviewViewModel
{
    public int HotelBookingId { get; set; }
    public string PetName { get; set; } = "";
    public string CageId { get; set; } = "";
    public DateTime CheckoutAt { get; set; }
    public decimal RoomAmount { get; set; }
    public decimal FoodAmount { get; set; }
    public decimal AddonAmount { get; set; }
    public decimal LateFeeAmount { get; set; }
    public decimal OtherAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string StatementStatus { get; set; } = "Draft";
    public string? OrderId { get; set; }
    public string? OrderStatus { get; set; }
    public bool CanFinalize { get; set; }
    public List<HotelCheckoutPreviewItem> Items { get; set; } = [];
}

public class HotelCheckoutPreviewItem
{
    public string ChargeType { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "";
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }
}
