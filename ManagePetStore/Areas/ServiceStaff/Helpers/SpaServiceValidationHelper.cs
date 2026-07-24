using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Http;

namespace ManagePetStore.Areas.ServiceStaff.Helpers
{
    /// <summary>
    /// Lớp Helper tập trung tất cả các quy tắc Validate dữ liệu cho phân hệ Dịch Vụ Spa (Phân hệ của Nhật Minh).
    /// Bao gồm: Danh mục Dịch vụ Spa, Đặt lịch Spa, Đánh giá dịch vụ Spa, Tiếp nhận Khách vãng lai, Hủy lịch hẹn Spa.
    /// </summary>
    public static class SpaServiceValidationHelper
    {
        private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg" };
        private const long MaxSizeBytes = 100L * 1024 * 1024; // Giới hạn 100MB cho ảnh đính kèm

        /// <summary>
        /// UC-33: Kiểm tra tính hợp lệ của thông tin Dịch vụ Spa cơ bản (Tên, thời lượng, đơn giá).
        /// </summary>
        public static (bool IsValid, string? ErrorMessage) ValidateBasicInfo(string name, int duration, decimal price)
        {
            if (string.IsNullOrWhiteSpace(name) || duration <= 0 || price < 0)
            {
                return (false, "Thông tin nhập vào không hợp lệ. Vui lòng kiểm tra lại tên, thời lượng và đơn giá.");
            }
            return (true, null);
        }

        /// <summary>
        /// UC-33 & UC-23: Kiểm tra tính hợp lệ của danh sách tệp ảnh quảng bá/đánh giá (Định dạng PNG, JPG, JPEG & dung lượng &lt;= 100MB/file).
        /// </summary>
        public static (bool IsValid, string? ErrorMessage) ValidateImageFiles(IEnumerable<IFormFile>? files)
        {
            if (files == null || !files.Any()) return (true, null);

            foreach (var file in files)
            {
                if (file.Length == 0) continue;

                var ext = Path.GetExtension(file.FileName);
                if (string.IsNullOrEmpty(ext) || !AllowedImageExtensions.Contains(ext))
                {
                    return (false, $"Tệp '{file.FileName}' không đúng định dạng. Hệ thống chỉ chấp nhận ảnh định dạng PNG, JPG hoặc JPEG.");
                }

                if (file.Length > MaxSizeBytes)
                {
                    return (false, $"Tệp '{file.FileName}' có dung lượng vượt quá giới hạn tối đa 100MB.");
                }
            }

            return (true, null);
        }

        /// <summary>
        /// UC-41: Kiểm tra tính hợp lệ khi Tiếp nhận / Chỉnh sửa thông tin Khách vãng lai tại quầy (Walk-in Pet).
        /// </summary>
        public static (bool IsValid, string? ErrorMessage) ValidateWalkInInfo(
            string petName, string customerName, string phone, int serviceId, decimal weight)
        {
            if (string.IsNullOrWhiteSpace(petName) || string.IsNullOrWhiteSpace(customerName) || string.IsNullOrWhiteSpace(phone) || serviceId <= 0)
            {
                return (false, "Vui lòng nhập đầy đủ thông tin bắt buộc.");
            }

            var cleanPhone = phone.Trim();
            if (cleanPhone.Length != 10 || !cleanPhone.All(char.IsDigit))
            {
                return (false, "Số điện thoại không hợp lệ. Số điện thoại phải gồm đúng 10 chữ số và không chứa ký tự chữ.");
            }

            if (weight <= 0 || weight > 200m)
            {
                return (false, "Cân nặng thú cưng phải lớn hơn 0 và không vượt quá 200 kg.");
            }

            return (true, null);
        }

        /// <summary>
        /// UC-43 & UC-47: Kiểm tra điều kiện khi Hủy lịch hẹn Spa (Cả phía Staff và phía Khách hàng).
        /// </summary>
        public static (bool IsValid, string? ErrorMessage) ValidateSpaCancellation(DateTime bookingDateTime, string currentSpaStatus, string? reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return (false, "Vui lòng nhập lý do hủy lịch hẹn.");
            }

            if (currentSpaStatus == "Cancelled")
            {
                return (false, "Lịch hẹn này đã được hủy trước đó.");
            }

            bool isPending = currentSpaStatus == "0" || currentSpaStatus.EndsWith("|0") || currentSpaStatus == "Tiếp nhận";
            if (!isPending)
            {
                return (false, "Lịch hẹn đã được nhân viên tiếp nhận bắt đầu thực hiện, không thể hủy.");
            }

            if (bookingDateTime <= DateTime.Now.AddHours(2))
            {
                return (false, "Không thể hủy lịch hẹn đã cận giờ thực hiện (cần hủy trước tối thiểu 2 tiếng).");
            }

            return (true, null);
        }

        /// <summary>
        /// UC-23: Kiểm tra tính hợp lệ khi gửi Đánh giá Dịch vụ Spa (Số sao 1-5 sao và tệp ảnh đính kèm).
        /// </summary>
        public static (bool IsValid, string? ErrorMessage) ValidateSpaReview(int ratingStar, IFormFile? reviewImage)
        {
            if (ratingStar < 1 || ratingStar > 5)
            {
                return (false, "Số sao đánh giá phải từ 1 đến 5 sao.");
            }

            if (reviewImage != null && reviewImage.Length > 0)
            {
                return ValidateImageFiles(new[] { reviewImage });
            }

            return (true, null);
        }
    }
}
