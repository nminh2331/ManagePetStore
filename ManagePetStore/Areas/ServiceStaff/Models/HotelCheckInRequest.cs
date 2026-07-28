using System.ComponentModel.DataAnnotations;
using ManagePetStore.Models;

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

    public bool FoodPriceChangeConfirmed { get; set; }

    public int? RoomTypeId { get; set; }

    [StringLength(20, ErrorMessage = "Mã chuồng không được vượt quá 20 ký tự.")]
    public string CageId { get; set; } = string.Empty;

    public DateTime? CheckOutDate { get; set; }

    [StringLength(50)]
    public string FoodProductSku { get; set; } = string.Empty;

    public int? HotelBookingId { get; set; }

    // [nam] Kiểm tra nguồn tiếp nhận, kết luận sức khoẻ và dữ liệu chuồng trước check-in.
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // [nam][Validate] Hiện hệ thống chỉ tiếp nhận từ sổ y tế có sẵn; giá trị khác có thể là request sửa tay.
        if (!string.Equals(ReceptionSource, ExistingMedicalRecordSource, StringComparison.Ordinal))
        {
            yield return new ValidationResult(
                "Hiện chỉ hỗ trợ tiếp nhận bằng sổ y tế có sẵn.",
                new[] { nameof(ReceptionSource) });
        }

        // [nam][BR] Kết luận sức khỏe chỉ có ba trạng thái chuẩn; Monitor và Rejected bắt buộc giải thích.
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

        // [nam][BR] Chỉ ghi nhận từ chối cho booking online đã tồn tại; lượt gửi trực tiếp chưa tạo booking để hủy.
        if (HealthStatus == RejectedStatus && !HotelBookingId.HasValue)
        {
            yield return new ValidationResult(
                "Chỉ lưu quyết định từ chối khi đang xử lý một booking đã có.",
                new[] { nameof(HotelBookingId) });
        }

        // [nam][Validate] Luồng được nhận mới bắt buộc đủ ngày trả, loại chuồng, chuồng và gói thức ăn.
        if (HealthStatus != RejectedStatus)
        {
            if (!CheckOutDate.HasValue)
            {
                yield return new ValidationResult(
                    "Ngày trả dự kiến là bắt buộc.",
                    new[] { nameof(CheckOutDate) });
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

        // [nam][BR] Checkbox xác nhận là bằng chứng Staff đã trực tiếp hoàn tất kiểm tra sức khỏe.
        if (!HealthCheckConfirmed)
        {
            yield return new ValidationResult(
                "Nhân viên phải xác nhận đã kiểm tra sức khỏe trước khi tiếp nhận.",
                new[] { nameof(HealthCheckConfirmed) });
        }

        // [nam][BR] Giờ nhận thật do server chốt; ngày trả phải nằm trong 365 ngày và giờ bàn giao của cửa hàng.
        if (HealthStatus != RejectedStatus && CheckOutDate.HasValue)
        {
            DateTime serverNow = DateTime.Now;
            if (CheckOutDate.Value <= serverNow)
            {
                yield return new ValidationResult(
                    "Ngày trả dự kiến phải sau thời gian tiếp nhận.",
                    new[] { nameof(CheckOutDate) });
            }
            else if ((CheckOutDate.Value - serverNow).TotalDays > 365)
            {
                yield return new ValidationResult(
                    "Thời gian lưu trú dự kiến không được vượt quá 365 ngày.",
                    new[] { nameof(CheckOutDate) });
            }

            if (!HotelOperatingHoursPolicy.IsExpectedCheckoutWithinHandoverHours(CheckOutDate.Value))
            {
                yield return new ValidationResult(
                    HotelOperatingHoursPolicy.ExpectedCheckoutError,
                    new[] { nameof(CheckOutDate) });
            }
        }

        // [nam][BR] Staff được tiếp nhận sớm hơn lịch đặt; service dùng giờ server và vẫn kiểm tra trùng lịch thực tế.
    }
}
