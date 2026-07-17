using System.ComponentModel.DataAnnotations;

namespace ManagePetStore.Areas.ServiceStaff.Models;

public class PrepareHotelCheckoutRequest : IValidatableObject
{
    [Range(1, int.MaxValue, ErrorMessage = "Lượt đặt chuồng không hợp lệ.")]
    public int HotelBookingId { get; set; }

    [StringLength(250, ErrorMessage = "Mô tả chi phí không được vượt quá 250 ký tự.")]
    public string? OtherDescription { get; set; }

    [Range(typeof(decimal), "0", "100000000", ErrorMessage = "Chi phí phát sinh phải từ 0 đến 100.000.000đ.")]
    public decimal OtherAmount { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (OtherAmount > 0 && string.IsNullOrWhiteSpace(OtherDescription))
        {
            yield return new ValidationResult(
                "Phải nhập mô tả khi có chi phí phát sinh.",
                [nameof(OtherDescription)]);
        }

        if (OtherAmount % 1000m != 0)
        {
            yield return new ValidationResult(
                "Chi phí phát sinh phải theo bước 1.000đ.",
                [nameof(OtherAmount)]);
        }
    }
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
    public bool CanReset { get; set; }
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
