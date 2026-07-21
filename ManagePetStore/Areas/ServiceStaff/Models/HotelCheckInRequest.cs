using System.ComponentModel.DataAnnotations;

namespace ManagePetStore.Areas.ServiceStaff.Models;

public sealed class HotelCheckInRequest : IValidatableObject
{
    public const string ExistingMedicalRecordSource = "ExistingMedicalRecord";
    public const string FitStatus = "Fit";
    public const string MonitorStatus = "Monitor";
    public const string RejectedStatus = "Rejected";

    [Required(ErrorMessage = "Phải chọn hình thức tiếp nhận.")]
    public string ReceptionSource { get; set; } = string.Empty;

    [Required(ErrorMessage = "Số điện thoại chủ thú cưng là bắt buộc.")]
    [RegularExpression(@"^0(?:[\s.-]?\d){9,10}$", ErrorMessage = "Số điện thoại phải bắt đầu bằng 0 và gồm 10-11 chữ số.")]
    public string CustomerPhone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phải chọn sổ y tế dùng để tiếp nhận.")]
    [Range(1, int.MaxValue, ErrorMessage = "Sổ y tế không hợp lệ.")]
    public int? MedicalRecordId { get; set; }

    [Required(ErrorMessage = "Phải chọn kết luận kiểm tra sức khỏe.")]
    public string HealthStatus { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "Ghi chú tiếp nhận không được vượt quá 1000 ký tự.")]
    public string? HealthNote { get; set; }

    public bool HealthCheckConfirmed { get; set; }

    public int? RoomTypeId { get; set; }

    [StringLength(20, ErrorMessage = "Mã chuồng không được vượt quá 20 ký tự.")]
    public string CageId { get; set; } = string.Empty;

    public DateTime? CheckInDate { get; set; }

    public DateTime? CheckOutDate { get; set; }

    [StringLength(50)]
    public string FoodProductSku { get; set; } = string.Empty;

    public int? HotelBookingId { get; set; }

    // [nam] Kiểm tra nguồn tiếp nhận, kết luận sức khoẻ và dữ liệu chuồng trước check-in.
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.Equals(ReceptionSource, ExistingMedicalRecordSource, StringComparison.Ordinal))
        {
            yield return new ValidationResult(
                "Hiện chỉ hỗ trợ tiếp nhận bằng sổ y tế có sẵn.",
                new[] { nameof(ReceptionSource) });
        }

        var validHealthStatuses = new[] { FitStatus, MonitorStatus, RejectedStatus };
        if (!validHealthStatuses.Contains(HealthStatus, StringComparer.Ordinal))
        {
            yield return new ValidationResult(
                "Kết luận kiểm tra sức khỏe không hợp lệ.",
                new[] { nameof(HealthStatus) });
        }

        if (HealthStatus == MonitorStatus && string.IsNullOrWhiteSpace(HealthNote))
        {
            yield return new ValidationResult(
                "Thú cưng cần theo dõi phải có ghi chú cho nhân viên chăm sóc.",
                new[] { nameof(HealthNote) });
        }

        if (HealthStatus == RejectedStatus && string.IsNullOrWhiteSpace(HealthNote))
        {
            yield return new ValidationResult(
                "Phải ghi rõ lý do không đủ điều kiện lưu trú.",
                new[] { nameof(HealthNote) });
        }

        if (HealthStatus == RejectedStatus && !HotelBookingId.HasValue)
        {
            yield return new ValidationResult(
                "Chỉ lưu quyết định từ chối khi đang xử lý một booking đã có.",
                new[] { nameof(HotelBookingId) });
        }

        if (HealthStatus != RejectedStatus)
        {
            if (!CheckInDate.HasValue)
            {
                yield return new ValidationResult(
                    "Ngày nhận là bắt buộc.",
                    new[] { nameof(CheckInDate) });
            }

            if (!RoomTypeId.HasValue || RoomTypeId.Value <= 0)
            {
                yield return new ValidationResult(
                    "Phải chọn loại chuồng.",
                    new[] { nameof(RoomTypeId) });
            }

            if (string.IsNullOrWhiteSpace(CageId))
            {
                yield return new ValidationResult(
                    "Phải chọn chuồng.",
                    new[] { nameof(CageId) });
            }

            if (string.IsNullOrWhiteSpace(FoodProductSku))
            {
                yield return new ValidationResult(
                    "Phải chọn gói thức ăn từ kho cửa hàng.",
                    new[] { nameof(FoodProductSku) });
            }
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

        if (CheckInDate.HasValue && CheckInDate.Value > DateTime.Now)
        {
            yield return new ValidationResult(
                "Ngày nhận chuồng không được sau thời điểm hiện tại.",
                new[] { nameof(CheckInDate) });
        }
    }
}
