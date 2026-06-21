using System.ComponentModel.DataAnnotations;

namespace ManagePetStore.Models.CustomerModels;

public class HotelBookingRequest : IValidatableObject
{
    [Required(ErrorMessage = "Vui lòng chọn thú cưng.")]
    [Range(1, int.MaxValue, ErrorMessage = "Thú cưng đã chọn không hợp lệ.")]
    public int? PetId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn loại phòng.")]
    [Range(1, int.MaxValue, ErrorMessage = "Loại phòng đã chọn không hợp lệ.")]
    public int? RoomTypeId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn ngày nhận phòng.")]
    [DataType(DataType.Date)]
    public DateTime? CheckInDate { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn ngày trả phòng.")]
    [DataType(DataType.Date)]
    public DateTime? CheckOutDate { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!CheckInDate.HasValue || !CheckOutDate.HasValue)
        {
            yield break;
        }

        var checkIn = CheckInDate.Value.Date;
        var checkOut = CheckOutDate.Value.Date;
        var latestAllowedCheckIn = DateTime.Today.AddDays(365);

        if (checkIn < DateTime.Today)
        {
            yield return new ValidationResult(
                "Ngày nhận phòng không được ở trong quá khứ.",
                [nameof(CheckInDate)]);
        }

        if (checkIn > latestAllowedCheckIn)
        {
            yield return new ValidationResult(
                "Chỉ có thể đặt phòng trước tối đa 365 ngày.",
                [nameof(CheckInDate)]);
        }

        if (checkOut <= checkIn)
        {
            yield return new ValidationResult(
                "Ngày trả phòng phải sau ngày nhận phòng.",
                [nameof(CheckOutDate)]);
            yield break;
        }

        if ((checkOut - checkIn).Days > 90)
        {
            yield return new ValidationResult(
                "Mỗi lượt đặt phòng không được vượt quá 90 đêm.",
                [nameof(CheckOutDate)]);
        }
    }
}

public class HotelBookingHistoryPageViewModel : CustomerSidebarViewModel
{
    public List<HotelBookingListItemViewModel> Bookings { get; set; } = [];
}

public class HotelBookingListItemViewModel
{
    public int HotelBookingId { get; set; }
    public string PetName { get; set; } = "";
    public string CageId { get; set; } = "";
    public string RoomTypeName { get; set; } = "";
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public int StayDays { get; set; }
    public decimal FinalAmount { get; set; }
    public string Status { get; set; } = "";
    public string StatusKey { get; set; } = "";
    public bool CanCancel { get; set; }
}

