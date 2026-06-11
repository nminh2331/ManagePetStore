using System.ComponentModel.DataAnnotations;

namespace ManagePetStore.SpaServices.Models;

public sealed class HotelCheckInRequest : IValidatableObject
{
    public const string FitStatus = "Fit";
    public const string MonitorStatus = "Monitor";
    public const string RejectedStatus = "Rejected";

    [Required(ErrorMessage = "Số điện thoại chủ thú cưng là bắt buộc.")]
    [RegularExpression(@"^0(?:[\s.-]?\d){9,10}$", ErrorMessage = "Số điện thoại phải bắt đầu bằng 0 và gồm 10-11 chữ số.")]
    public string CustomerPhone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Họ tên chủ thú cưng là bắt buộc.")]
    [StringLength(100, ErrorMessage = "Họ tên chủ thú cưng không được vượt quá 100 ký tự.")]
    public string CustomerName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tên thú cưng là bắt buộc.")]
    [StringLength(50, ErrorMessage = "Tên thú cưng không được vượt quá 50 ký tự.")]
    public string PetName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Loài thú cưng là bắt buộc.")]
    [StringLength(30, ErrorMessage = "Tên loài không được vượt quá 30 ký tự.")]
    public string Species { get; set; } = string.Empty;

    [StringLength(50, ErrorMessage = "Giống thú cưng không được vượt quá 50 ký tự.")]
    public string? Breed { get; set; }

    [StringLength(30, ErrorMessage = "Tuổi thú cưng không được vượt quá 30 ký tự.")]
    public string? Age { get; set; }

    [Range(typeof(decimal), "0.1", "200", ErrorMessage = "Cân nặng phải nằm trong khoảng 0,1-200 kg.")]
    public decimal Weight { get; set; }

    [StringLength(1000, ErrorMessage = "Bệnh lý/tình trạng đặc biệt không được vượt quá 1000 ký tự.")]
    public string? Pathology { get; set; }

    [Required(ErrorMessage = "Phải chọn kết luận kiểm tra sức khỏe.")]
    public string HealthStatus { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nhiệt độ cơ thể là bắt buộc.")]
    [Range(typeof(decimal), "30", "45", ErrorMessage = "Nhiệt độ cơ thể phải nằm trong khoảng 30-45°C.")]
    public decimal? BodyTemperature { get; set; }

    [Required(ErrorMessage = "Ghi chú kiểm tra sức khỏe là bắt buộc.")]
    [StringLength(1000, MinimumLength = 5, ErrorMessage = "Ghi chú kiểm tra sức khỏe phải từ 5-1000 ký tự.")]
    public string HealthNote { get; set; } = string.Empty;

    public bool HealthCheckConfirmed { get; set; }

    [Required(ErrorMessage = "Phải chọn loại chuồng.")]
    [Range(1, int.MaxValue, ErrorMessage = "Loại chuồng không hợp lệ.")]
    public int? RoomTypeId { get; set; }

    [Required(ErrorMessage = "Phải chọn chuồng.")]
    [StringLength(20, ErrorMessage = "Mã chuồng không được vượt quá 20 ký tự.")]
    public string CageId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ngày nhận là bắt buộc.")]
    public DateTime? CheckInDate { get; set; }

    public DateTime? CheckOutDate { get; set; }

    public int? ExistingPetId { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var validSpecies = new[] { "Chó", "Mèo", "Thỏ", "Hamster", "Khác" };
        if (!validSpecies.Contains(Species, StringComparer.Ordinal))
        {
            yield return new ValidationResult(
                "Loài thú cưng không hợp lệ.",
                new[] { nameof(Species) });
        }

        var validHealthStatuses = new[] { FitStatus, MonitorStatus, RejectedStatus };
        if (!validHealthStatuses.Contains(HealthStatus, StringComparer.Ordinal))
        {
            yield return new ValidationResult(
                "Kết luận kiểm tra sức khỏe không hợp lệ.",
                new[] { nameof(HealthStatus) });
        }

        if (HealthStatus == MonitorStatus && string.IsNullOrWhiteSpace(Pathology))
        {
            yield return new ValidationResult(
                "Thú cưng cần theo dõi phải ghi rõ bệnh lý hoặc tình trạng đặc biệt.",
                new[] { nameof(Pathology) });
        }

        if (!HealthCheckConfirmed)
        {
            yield return new ValidationResult(
                "Nhân viên phải xác nhận đã kiểm tra sức khỏe trước khi tiếp nhận.",
                new[] { nameof(HealthCheckConfirmed) });
        }

        if (CheckInDate.HasValue && CheckOutDate.HasValue)
        {
            if (CheckOutDate.Value <= CheckInDate.Value)
            {
                yield return new ValidationResult(
                    "Ngày trả dự kiến phải sau ngày nhận.",
                    new[] { nameof(CheckOutDate) });
            }
            else if ((CheckOutDate.Value - CheckInDate.Value).TotalDays > 365)
            {
                yield return new ValidationResult(
                    "Thời gian lưu trú dự kiến không được vượt quá 365 ngày.",
                    new[] { nameof(CheckOutDate) });
            }
        }

        if (CheckInDate.HasValue &&
            (CheckInDate.Value < DateTime.Now.AddDays(-1) || CheckInDate.Value > DateTime.Now.AddDays(1)))
        {
            yield return new ValidationResult(
                "Ngày nhận phải nằm trong vòng 24 giờ so với thời điểm hiện tại.",
                new[] { nameof(CheckInDate) });
        }
    }
}
