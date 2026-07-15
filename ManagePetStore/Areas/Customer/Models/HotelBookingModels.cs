using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ManagePetStore.Areas.Customer.Models;

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

    [Required(ErrorMessage = "Vui lòng chọn gói thức ăn cho thời gian lưu trú.")]
    [StringLength(50)]
    public string FoodProductSku { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? FeedingInstructions { get; set; }

    [StringLength(1000)]
    public string? AllergyNotes { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(FeedingInstructions))
        {
            var mealTimes = Regex.Split(
                    FeedingInstructions.Trim(),
                    @"\s*(?:,|;|và|and)\s*",
                    RegexOptions.IgnoreCase)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray();
            var parsedTimes = new List<TimeOnly>();

            if (mealTimes.Length == 0)
            {
                yield return new ValidationResult(
                    "Giờ ăn phải có dạng HH:mm, ví dụ 07:00 và 18:00.",
                    [nameof(FeedingInstructions)]);
            }

            foreach (var value in mealTimes)
            {
                if (!TimeOnly.TryParseExact(
                        value,
                        ["H:mm", "HH:mm"],
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var mealTime))
                {
                    yield return new ValidationResult(
                        "Giờ ăn phải có dạng HH:mm, ví dụ 07:00 và 18:00.",
                        [nameof(FeedingInstructions)]);
                    break;
                }

                parsedTimes.Add(mealTime);
            }

            if (parsedTimes.Count == mealTimes.Length &&
                parsedTimes.Any(time => time < new TimeOnly(7, 0) || time > new TimeOnly(20, 0)))
            {
                yield return new ValidationResult(
                    "Giờ ăn chỉ được trong khoảng 07:00 đến 20:00.",
                    [nameof(FeedingInstructions)]);
            }
        }

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
}
