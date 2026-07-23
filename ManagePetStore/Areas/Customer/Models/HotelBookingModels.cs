using System.ComponentModel.DataAnnotations;

namespace ManagePetStore.Areas.Customer.Models;

public class HotelBookingRequest : IValidatableObject
{
    [Required(ErrorMessage = "Vui lòng chọn thú cưng.")]
    [Range(1, int.MaxValue, ErrorMessage = "Thú cưng đã chọn không hợp lệ.")]
    public int? PetId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn loại phòng.")]
    [Range(1, int.MaxValue, ErrorMessage = "Loại phòng đã chọn không hợp lệ.")]
    public int? RoomTypeId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn chuồng còn trống.")]
    [StringLength(20, ErrorMessage = "Mã chuồng không được vượt quá 20 ký tự.")]
    public string CageId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn thời gian nhận phòng.")]
    [DataType(DataType.DateTime)]
    public DateTime? CheckInDate { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn thời gian trả phòng.")]
    [DataType(DataType.DateTime)]
    public DateTime? CheckOutDate { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn gói thức ăn cho thời gian lưu trú.")]
    [StringLength(50)]
    public string FoodProductSku { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? AllergyNotes { get; set; }

    // [nam] Kiểm tra mốc 15 phút, thời gian đặt và thời lượng lưu trú tối đa.
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!CheckInDate.HasValue || !CheckOutDate.HasValue)
        {
            yield break;
        }

        var checkIn = CheckInDate.Value;
        var checkOut = CheckOutDate.Value;
        var now = DateTime.Now;
        var latestAllowedCheckIn = now.AddDays(365);

        if (checkIn.Minute % 15 != 0 || checkIn.Second != 0 ||
            checkOut.Minute % 15 != 0 || checkOut.Second != 0)
        {
            yield return new ValidationResult(
                "Thời gian nhận và trả phòng phải theo từng mốc 15 phút.",
                [nameof(CheckInDate), nameof(CheckOutDate)]);
        }

        if (checkIn < now)
        {
            yield return new ValidationResult(
                "Thời gian nhận phòng không được ở trong quá khứ.",
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
                "Thời gian trả phòng phải sau thời gian nhận phòng.",
                [nameof(CheckOutDate)]);
            yield break;
        }

        if ((checkOut - checkIn).TotalHours > 90 * 24)
        {
            yield return new ValidationResult(
                "Mỗi lượt đặt phòng không được vượt quá 90 ngày.",
                [nameof(CheckOutDate)]);
        }
    }
}

public class HotelBookingHistoryPageViewModel : CustomerSidebarViewModel
{
    public List<HotelBookingListItemViewModel> Bookings { get; set; } = [];
    public List<HotelBookingListItemViewModel> VisibleBookings { get; set; } = [];
    public string SearchTerm { get; set; } = "";
    public string StatusFilter { get; set; } = "all";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 5;
    public int TotalFilteredItems { get; set; }
    public int TotalPages { get; set; }
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}

public class HotelBookingListItemViewModel
{
    public int HotelBookingId { get; set; }
    public string DisplayBookingId => $"HB{HotelBookingId:0000}";
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
    public bool ShowCannotCancelOnline { get; set; }
}

public class HotelBookingDetailPageViewModel : CustomerSidebarViewModel
{
    public ManagePetStore.Models.HotelBookingHistoryDetailViewModel Booking { get; set; } = new();
    public bool CanRequestCageChange { get; set; }
    public HotelCageChangeRequestItemViewModel? PendingCageChangeRequest { get; set; }
    public List<HotelCageChangeOptionViewModel> AvailableCages { get; set; } = [];
}

public class HotelCageChangeOptionViewModel
{
    public string CageId { get; set; } = string.Empty;
    public string RoomTypeName { get; set; } = string.Empty;
    public string RoomTypeCode { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public decimal DailyPrice { get; set; }
    public decimal EstimatedPriceDifference { get; set; }
}

public class HotelCageChangeRequestItemViewModel
{
    public int ChangeRequestId { get; set; }
    public string SourceCageId { get; set; } = string.Empty;
    public string TargetCageId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal EstimatedPriceDifference { get; set; }
    public DateTime RequestedAt { get; set; }
}
