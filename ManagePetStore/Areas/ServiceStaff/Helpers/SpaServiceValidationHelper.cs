using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Http;

namespace ManagePetStore.Areas.ServiceStaff.Helpers
{
    /// <summary>
    /// =========================================================================================
    /// NGƯỜI THỰC HIỆN / TÁC GIẢ: NHẬT MINH
    /// CHỨC NĂNG: Lớp Helper tập trung tất cả các quy tắc Validate dữ liệu cho phân hệ Dịch Vụ Spa.
    /// BAO GỒM CÁC UC CỦA NHẬT MINH:
    /// - UC-21: View Spa Services List (Xem danh sách dịch vụ Spa)
    /// - UC-22: Book Spa Appointment (Đặt lịch hẹn Spa)
    /// - UC-23: Service Rating & Review (Đánh giá dịch vụ Spa)
    /// - UC-25: Spa Invoice Payment (Thanh toán hóa đơn Spa)
    /// - UC-33: Manage Spa Services (Quản lý danh mục Dịch vụ Spa)
    /// - UC-41: Receive Walk-in Pet (Tiếp nhận khách vãng lai tại quầy)
    /// - UC-43: Cancel Booking as Staff (Nhân viên hủy lịch hẹn Spa)
    /// - UC-47: Cancel Booking as Customer (Khách hàng hủy lịch hẹn Spa)
    /// =========================================================================================
    /// </summary>
    public static class SpaServiceValidationHelper
    {
        // Định dạng tệp ảnh đính kèm hợp lệ do Nhật Minh quy định
        private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg" };
        
        // Dung lượng tối đa 100MB cho tệp ảnh đính kèm do Nhật Minh quy định
        private const long MaxSizeBytes = 100L * 1024 * 1024;

        /// <summary>
        /// NGƯỜI THỰC HIỆN: Nhật Minh
        /// CHỨC NĂNG: Validate (kiểm tra tính hợp lệ) thông tin Dịch vụ Spa cơ bản khi Thêm/Sửa danh mục (UC-33).
        /// CÁC QUY TẮC VALIDATE CỦA NHẬT MINH:
        /// 1. Tên dịch vụ không được để trống hoặc chỉ chứa khoảng trắng.
        /// 2. Thời lượng thực hiện (phút) phải lớn hơn 0.
        /// 3. Đơn giá dịch vụ (VNĐ) không được nhỏ hơn 0.
        /// </summary>
        /// <param name="name">Tên dịch vụ Spa cần kiểm tra</param>
        /// <param name="duration">Thời lượng dịch vụ tính theo phút</param>
        /// <param name="price">Đơn giá dịch vụ tính theo VNĐ</param>
        /// <returns>Trả về tuple (IsValid: Hợp lệ hay không, ErrorMessage: Thông báo lỗi tiếng Việt nếu không hợp lệ)</returns>
        public static (bool IsValid, string? ErrorMessage) ValidateBasicInfo(string name, int duration, decimal price)
        {
            // Kiểm tra tên không rỗng, thời lượng > 0 và đơn giá không âm
            if (string.IsNullOrWhiteSpace(name) || duration <= 0 || price < 0)
            {
                return (false, "Thông tin nhập vào không hợp lệ. Vui lòng kiểm tra lại tên, thời lượng và đơn giá.");
            }
            return (true, null);
        }

        /// <summary>
        /// NGƯỜI THỰC HIỆN: Nhật Minh
        /// CHỨC NĂNG: Validate (kiểm tra tính hợp lệ) danh sách các tệp hình ảnh đính kèm cho Dịch vụ Spa & Đánh giá (UC-33 & UC-23).
        /// CÁC QUY TẮC VALIDATE CỦA NHẬT MINH:
        /// 1. Bỏ qua các tệp có dung lượng 0 byte.
        /// 2. Kiểm tra phần mở rộng (đuôi tệp): Chỉ chấp nhận PNG, JPG, JPEG.
        /// 3. Kiểm tra dung lượng tệp: Không được vượt quá 100MB per file.
        /// </summary>
        /// <param name="files">Danh sách tệp hình ảnh được tải lên</param>
        /// <returns>Trả về tuple (IsValid: Hợp lệ hay không, ErrorMessage: Thông báo lỗi tiếng Việt)</returns>
        public static (bool IsValid, string? ErrorMessage) ValidateImageFiles(IEnumerable<IFormFile>? files)
        {
            if (files == null || !files.Any()) return (true, null);

            foreach (var file in files)
            {
                if (file.Length == 0) continue;

                // Nhật Minh Validate: Đuôi file ảnh phải thuộc danh sách PNG, JPG, JPEG
                var ext = Path.GetExtension(file.FileName);
                if (string.IsNullOrEmpty(ext) || !AllowedImageExtensions.Contains(ext))
                {
                    return (false, $"Tệp '{file.FileName}' không đúng định dạng. Hệ thống chỉ chấp nhận ảnh định dạng PNG, JPG hoặc JPEG.");
                }

                // Nhật Minh Validate: Dung lượng tệp ảnh không được vượt quá 100MB
                if (file.Length > MaxSizeBytes)
                {
                    return (false, $"Tệp '{file.FileName}' có dung lượng vượt quá giới hạn tối đa 100MB.");
                }
            }

            return (true, null);
        }

        /// <summary>
        /// NGƯỜI THỰC HIỆN: Nhật Minh
        /// CHỨC NĂNG: Validate (kiểm tra tính hợp lệ) dữ liệu đầu vào khi Tiếp nhận / Chỉnh sửa thông tin Khách vãng lai tại quầy (UC-41).
        /// CÁC QUY TẮC VALIDATE CỦA NHẬT MINH:
        /// 1. Tất cả các trường bắt buộc (Tên thú cưng, Tên khách hàng, Số điện thoại, Mã dịch vụ) không được để trống.
        /// 2. Số điện thoại phải có đúng 10 chữ số và là ký tự số.
        /// 3. Cân nặng thú cưng (kg) phải lớn hơn 0 và không vượt quá 200kg.
        /// </summary>
        /// <param name="petName">Tên thú cưng</param>
        /// <param name="customerName">Tên chủ nuôi (Khách vãng lai)</param>
        /// <param name="phone">Số điện thoại liên hệ</param>
        /// <param name="serviceId">Mã dịch vụ Spa được chọn</param>
        /// <param name="weight">Cân nặng thú cưng (kg)</param>
        /// <returns>Trả về tuple (IsValid: Hợp lệ hay không, ErrorMessage: Thông báo lỗi tiếng Việt)</returns>
        public static (bool IsValid, string? ErrorMessage) ValidateWalkInInfo(
            string petName, string customerName, string phone, int serviceId, decimal weight)
        {
            // Nhật Minh Validate: Kiểm tra trường rỗng hoặc dịch vụ <= 0
            if (string.IsNullOrWhiteSpace(petName) || string.IsNullOrWhiteSpace(customerName) || string.IsNullOrWhiteSpace(phone) || serviceId <= 0)
            {
                return (false, "Vui lòng nhập đầy đủ thông tin bắt buộc.");
            }

            // Nhật Minh Validate: Đảm bảo số điện thoại gồm đúng 10 chữ số
            var cleanPhone = phone.Trim();
            if (cleanPhone.Length != 10 || !cleanPhone.All(char.IsDigit))
            {
                return (false, "Số điện thoại không hợp lệ. Số điện thoại phải gồm đúng 10 chữ số và không chứa ký tự chữ.");
            }

            // Nhật Minh Validate: Cân nặng trong khoảng hợp lệ (0kg < Cân nặng <= 200kg)
            if (weight <= 0 || weight > 200m)
            {
                return (false, "Cân nặng thú cưng phải lớn hơn 0 và không vượt quá 200 kg.");
            }

            return (true, null);
        }

        /// <summary>
        /// NGƯỜI THỰC HIỆN: Nhật Minh
        /// CHỨC NĂNG: Validate (kiểm tra tính hợp lệ) điều kiện Hủy lịch hẹn Spa (UC-43 & UC-47).
        /// CÁC QUY TẮC VALIDATE CỦA NHẬT MINH:
        /// 1. Lý do hủy lịch hẹn không được để trống.
        /// 2. Lịch hẹn chưa bị hủy từ trước (SpaStatus != "Cancelled").
        /// 3. Lịch hẹn phải ở trạng thái chờ/tiếp nhận ("0", "|0", "Tiếp nhận"). Nếu đã bắt đầu thực hiện thì không cho hủy.
        /// 4. Phải hủy trước giờ hẹn tối thiểu 2 tiếng.
        /// </summary>
        /// <param name="bookingDateTime">Thời gian bắt đầu lịch hẹn</param>
        /// <param name="currentSpaStatus">Trạng thái hiện tại của ca Spa</param>
        /// <param name="reason">Lý do hủy do người dùng nhập</param>
        /// <returns>Trả về tuple (IsValid: Hợp lệ hay không, ErrorMessage: Thông báo lỗi tiếng Việt)</returns>
        public static (bool IsValid, string? ErrorMessage) ValidateSpaCancellation(DateTime bookingDateTime, string currentSpaStatus, string? reason)
        {
            // Nhật Minh Validate: Kiểm tra lý do hủy không được rỗng
            if (string.IsNullOrWhiteSpace(reason))
            {
                return (false, "Vui lòng nhập lý do hủy lịch hẹn.");
            }

            // Nhật Minh Validate: Kiểm tra lịch hẹn chưa bị hủy từ trước
            if (currentSpaStatus == "Cancelled")
            {
                return (false, "Lịch hẹn này đã được hủy trước đó.");
            }

            // Nhật Minh Validate: Chỉ cho phép hủy khi ca Spa ở trạng thái chờ (Tiếp nhận/bước 0)
            bool isPending = currentSpaStatus == "0" || currentSpaStatus.EndsWith("|0") || currentSpaStatus == "Tiếp nhận";
            if (!isPending)
            {
                return (false, "Lịch hẹn đã được nhân viên tiếp nhận bắt đầu thực hiện, không thể hủy.");
            }

            // Nhật Minh Validate: Thời gian hủy phải trước mốc hẹn ít nhất 2 giờ
            if (bookingDateTime <= DateTime.Now.AddHours(2))
            {
                return (false, "Không thể hủy lịch hẹn đã cận giờ thực hiện (cần hủy trước tối thiểu 2 tiếng).");
            }

            return (true, null);
        }

        /// <summary>
        /// NGƯỜI THỰC HIỆN: Nhật Minh
        /// CHỨC NĂNG: Validate (kiểm tra tính hợp lệ) thông tin khi Khách hàng gửi Đánh giá dịch vụ Spa (UC-23).
        /// CÁC QUY TẮC VALIDATE CỦA NHẬT MINH:
        /// 1. Số sao đánh giá (RatingStar) phải nằm trong khoảng từ 1 đến 5 sao.
        /// 2. Tệp ảnh đính kèm (nếu có) phải thỏa mãn quy tắc định dạng và dung lượng ảnh của Nhật Minh.
        /// </summary>
        /// <param name="ratingStar">Số sao đánh giá (1 - 5)</param>
        /// <param name="reviewImage">Tệp ảnh đính kèm đánh giá (nếu có)</param>
        /// <returns>Trả về tuple (IsValid: Hợp lệ hay không, ErrorMessage: Thông báo lỗi tiếng Việt)</returns>
        public static (bool IsValid, string? ErrorMessage) ValidateSpaReview(int ratingStar, IFormFile? reviewImage)
        {
            // Nhật Minh Validate: Số sao đánh giá từ 1 đến 5 sao
            if (ratingStar < 1 || ratingStar > 5)
            {
                return (false, "Số sao đánh giá phải từ 1 đến 5 sao.");
            }

            // Nhật Minh Validate: Kiểm tra ảnh đính kèm thông qua hàm ValidateImageFiles
            if (reviewImage != null && reviewImage.Length > 0)
            {
                return ValidateImageFiles(new[] { reviewImage });
            }

            return (true, null);
        }
    }
}
