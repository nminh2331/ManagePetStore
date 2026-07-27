using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using ManagePetStore.Hubs;
using ManagePetStore.Models;
using ManagePetStore.Areas.ServiceStaff.Models;
using ManagePetStore.Services;
using ManagePetStore.Services.Warehouse;
using CustomerEntity = ManagePetStore.Models.Customer;

namespace ManagePetStore.Areas.ServiceStaff.Controllers
{
    public partial class SpaCagesController
    {
        // [nam] Kiểm tra giới hạn giá ngày và giá giờ của loại chuồng.
        private static string? ValidateRoomTypePricing(decimal dailyPrice, decimal hourlyPrice)
        {
            // [nam][BR] Giá ngày và phí quá giờ phải nằm trong khung cấu hình nghiệp vụ của Hotel.
            if (dailyPrice < MinimumRoomTypeDailyPrice)
            {
                return "Giá theo ngày không được thấp hơn 150.000đ.";
            }

            if (hourlyPrice < MinimumRoomTypeHourlyPrice)
            {
                return "Phí quá giờ không được thấp hơn 40.000đ.";
            }

            if (dailyPrice > MaximumRoomTypePrice || hourlyPrice > MaximumRoomTypePrice)
            {
                return "Giá chuồng không được vượt quá 100.000.000đ.";
            }

            // [nam][BR] Một giờ phụ thu không được đắt hơn trọn một ngày và mọi giá dùng bước 1.000đ.
            if (hourlyPrice > dailyPrice)
            {
                return "Phí quá giờ không được lớn hơn giá theo ngày.";
            }

            if (dailyPrice % 1000m != 0 || hourlyPrice % 1000m != 0)
            {
                return "Giá chuồng phải theo bước 1.000đ.";
            }

            return null;
        }


        // [nam] Kiểm tra tên, kích thước và sức chứa của loại chuồng.
        private static string? ValidateRoomTypeDetails(string? type, string? size, int capacity)
        {
            // [nam][Validate] Giới hạn độ dài và sức chứa bảo vệ cả DB lẫn bố cục màn danh mục.
            if (string.IsNullOrWhiteSpace(type) || type.Trim().Length > 100)
            {
                return "Tên loại chuồng là bắt buộc và không được vượt quá 100 ký tự.";
            }

            if (string.IsNullOrWhiteSpace(size) || size.Trim().Length > 50)
            {
                return "Kích cỡ chuồng là bắt buộc và không được vượt quá 50 ký tự.";
            }

            if (capacity is < 1 or > MaximumRoomTypeCapacity)
            {
                return "Sức chứa phải từ 1 đến 10 thú cưng.";
            }

            return null;
        }

        // [nam] Thêm loại chuồng mới sau khi kiểm tra thông tin và mức giá.
        [HttpPost("AddRoomType")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddRoomType(
            string type, string size, int capacity,
            decimal hourlyPrice, decimal dailyPrice,
            bool hasAc, bool hasCamera, bool hasPremiumFood)
        {
            var detailsError = ValidateRoomTypeDetails(type, size, capacity);
            if (detailsError != null)
            {
                TempData["HotelError"] = detailsError;
                return RedirectToAction(nameof(CageCategories));
            }

            var pricingError = ValidateRoomTypePricing(dailyPrice, hourlyPrice);
            if (pricingError != null)
            {
                TempData["HotelError"] = pricingError;
                return RedirectToAction(nameof(CageCategories));
            }

            // [nam][BR] Tên loại chuồng là duy nhất không phân biệt hoa/thường.
            if (await _context.RoomTypes.AnyAsync(r => r.Type.ToLower() == type.Trim().ToLower()))
            {
                TempData["HotelError"] = "Tên loại chuồng này đã tồn tại.";
                return RedirectToAction(nameof(CageCategories));
            }

            var roomType = new RoomType
            {
                Type = type.Trim(),
                Size = size?.Trim() ?? "Tiêu chuẩn",
                Capacity = capacity,
                HourlyPrice = hourlyPrice,
                DailyPrice = dailyPrice,
                HasAc = hasAc,
                HasCamera = hasCamera,
                HasPremiumFood = hasPremiumFood,
                Status = true
            };

            _context.RoomTypes.Add(roomType);
            await _context.SaveChangesAsync();

            TempData["HotelSuccess"] = $"Thêm loại chuồng '{type}' thành công!";
            return RedirectToAction(nameof(CageCategories));
        }

        // [nam] Cập nhật cấu hình và giá của một loại chuồng hiện có.
        [HttpPost("EditRoomType")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditRoomType(
            int id, string type, string size, int capacity,
            decimal hourlyPrice, decimal dailyPrice,
            bool hasAc, bool hasCamera, bool hasPremiumFood)
        {
            var roomType = await _context.RoomTypes.FindAsync(id);
            if (roomType == null)
            {
                TempData["HotelError"] = "Không tìm thấy loại chuồng.";
                return RedirectToAction(nameof(CageCategories));
            }

            var detailsError = ValidateRoomTypeDetails(type, size, capacity);
            if (detailsError != null)
            {
                TempData["HotelError"] = detailsError;
                return RedirectToAction(nameof(CageCategories));
            }

            var pricingError = ValidateRoomTypePricing(dailyPrice, hourlyPrice);
            if (pricingError != null)
            {
                TempData["HotelError"] = pricingError;
                return RedirectToAction(nameof(CageCategories));
            }

            if (await _context.RoomTypes.AnyAsync(r => r.Type.ToLower() == type.Trim().ToLower() && r.RoomTypeId != id))
            {
                TempData["HotelError"] = "Tên loại chuồng này đã tồn tại.";
                return RedirectToAction(nameof(CageCategories));
            }

            roomType.Type = type.Trim();
            roomType.Size = size?.Trim() ?? "Tiêu chuẩn";
            roomType.Capacity = capacity;
            roomType.HourlyPrice = hourlyPrice;
            roomType.DailyPrice = dailyPrice;
            roomType.HasAc = hasAc;
            roomType.HasCamera = hasCamera;
            roomType.HasPremiumFood = hasPremiumFood;

            await _context.SaveChangesAsync();
            TempData["HotelSuccess"] = "Cập nhật loại chuồng thành công!";
            return RedirectToAction(nameof(CageCategories));
        }

        // [nam] Xóa loại chuồng khi chưa có chuồng hoặc booking liên quan.
        [HttpPost("DeleteRoomType")]
        public async Task<IActionResult> DeleteRoomType(int id)
        {
            var roomType = await _context.RoomTypes.FindAsync(id);
            if (roomType == null)
                return Json(new { success = false, message = "Không tìm thấy loại chuồng." });

            // [nam][BR] Loại đã được tham chiếu chỉ ngừng hoạt động; xóa vật lý sẽ làm mất liên kết lịch sử.
            bool hasCages = await _context.Cages.AnyAsync(c => c.RoomTypeId == id);
            bool hasOrders = await _context.OrderItems.AnyAsync(o => o.RoomTypeId == id);

            if (hasCages || hasOrders)
            {
                roomType.Status = false;
                await _context.SaveChangesAsync();
                return Json(new { success = true, isSoftDeleted = true, message = "Loại chuồng đang được sử dụng, đã tự động chuyển sang trạng thái Ngưng hoạt động!" });
            }

            _context.RoomTypes.Remove(roomType);
            await _context.SaveChangesAsync();
            return Json(new { success = true, isSoftDeleted = false, message = "Xóa loại chuồng thành công!" });
        }

        // [nam] Bật hoặc tắt khả năng sử dụng loại chuồng trong hệ thống.
        [HttpPost("ToggleRoomType")]
        public async Task<IActionResult> ToggleRoomType(int id)
        {
            var roomType = await _context.RoomTypes.FindAsync(id);
            if (roomType == null)
                return Json(new { success = false, message = "Không tìm thấy loại chuồng." });

            roomType.Status = !roomType.Status;
            await _context.SaveChangesAsync();
            return Json(new { success = true, status = roomType.Status });
        }

        // [nam] Thêm chuồng mới vào một loại chuồng hợp lệ.
        [HttpPost("AddCage")]
        public async Task<IActionResult> AddCage(
            string cageId, int roomTypeId, string feedSchedule, int portion)
        {
            // [nam][Validate] CageId là khóa nghiệp vụ tối đa 20 ký tự; RoomTypeId phải là khóa dương.
            if (string.IsNullOrWhiteSpace(cageId) || cageId.Trim().Length > 20 || roomTypeId <= 0)
            {
                TempData["HotelError"] = "Mã chuồng là bắt buộc và không được vượt quá 20 ký tự.";
                return RedirectToAction(nameof(CageCategories));
            }

            if (feedSchedule?.Trim().Length > 100)
            {
                TempData["HotelError"] = "Lịch cho ăn không được vượt quá 100 ký tự.";
                return RedirectToAction(nameof(CageCategories));
            }

            // [nam][BR] Khẩu phần cấu hình theo gram trong khung 10-10.000 và bước 10 gram.
            if (portion is < MinimumCagePortionGrams or > MaximumCagePortionGrams || portion % 10 != 0)
            {
                TempData["HotelError"] = "Khẩu phần phải từ 10 đến 10.000 gram và theo bước 10 gram.";
                return RedirectToAction(nameof(CageCategories));
            }

            if (await _context.Cages.AnyAsync(c => c.CageId == cageId.Trim().ToUpper()))
            {
                TempData["HotelError"] = $"Mã chuồng '{cageId}' đã tồn tại.";
                return RedirectToAction(nameof(CageCategories));
            }

            // [nam][Validate] Không cho tạo chuồng dưới loại đã khóa hoặc ngoài ba mã Hotel hỗ trợ.
            var roomType = await _context.RoomTypes.FirstOrDefaultAsync(item =>
                item.RoomTypeId == roomTypeId &&
                item.Status &&
                HotelRoomTypeCatalog.Codes.Contains(item.Code));
            if (roomType == null)
            {
                TempData["HotelError"] = "Chỉ được thêm chuồng vào Standard, VIP hoặc Luxury đang hoạt động.";
                return RedirectToAction(nameof(CageCategories));
            }

            var cage = new Cage
            {
                CageId = cageId.Trim().ToUpper(),
                RoomTypeId = roomTypeId,
                Status = "Trống",
                FeedSchedule = feedSchedule?.Trim() ?? "08:00, 12:00, 18:00",
                Portion = portion
            };

            _context.Cages.Add(cage);
            await _context.SaveChangesAsync();

            TempData["HotelSuccess"] = $"Thêm chuồng {cage.CageId} thành công!";
            return RedirectToAction(nameof(CageCategories));
        }

        // [nam] Cập nhật loại, trạng thái và thông tin của chuồng.
        [HttpPost("EditCage")]
        public async Task<IActionResult> EditCage(
            string cageId, int roomTypeId, string feedSchedule, int portion)
        {
            var cage = await _context.Cages.FindAsync(cageId);
            if (cage == null)
            {
                TempData["HotelError"] = "Không tìm thấy chuồng.";
                return RedirectToAction(nameof(CageCategories));
            }

            if (feedSchedule?.Trim().Length > 100)
            {
                TempData["HotelError"] = "Lịch cho ăn không được vượt quá 100 ký tự.";
                return RedirectToAction(nameof(CageCategories));
            }

            if (portion is < MinimumCagePortionGrams or > MaximumCagePortionGrams || portion % 10 != 0)
            {
                TempData["HotelError"] = "Khẩu phần phải từ 10 đến 10.000 gram và theo bước 10 gram.";
                return RedirectToAction(nameof(CageCategories));
            }

            var roomType = await _context.RoomTypes.FirstOrDefaultAsync(item =>
                item.RoomTypeId == roomTypeId &&
                item.Status &&
                HotelRoomTypeCatalog.Codes.Contains(item.Code));
            if (roomType == null)
            {
                TempData["HotelError"] = "Chỉ được chuyển chuồng sang Standard, VIP hoặc Luxury đang hoạt động.";
                return RedirectToAction(nameof(CageCategories));
            }

            cage.RoomTypeId = roomTypeId;
            cage.FeedSchedule = feedSchedule?.Trim() ?? cage.FeedSchedule;
            cage.Portion = portion;

            await _context.SaveChangesAsync();
            TempData["HotelSuccess"] = $"Cập nhật chuồng {cageId} thành công!";
            return RedirectToAction(nameof(CageCategories));
        }

        // [nam] Xóa chuồng khi không có dữ liệu lưu trú đang tham chiếu.
        [HttpPost("DeleteCage")]
        public async Task<IActionResult> DeleteCage(string cageId)
        {
            var cage = await _context.Cages.FindAsync(cageId);
            if (cage == null)
                return Json(new { success = false, message = "Không tìm thấy chuồng." });

            // [nam][BR] Chỉ xóa vật lý chuồng trống chưa từng có booking để bảo toàn lịch sử lưu trú.
            if (cage.Status != "Trống")
                return Json(new { success = false, message = "Không thể xóa chuồng đang có thú cưng." });

            bool hasBookings = await _context.HotelBookings.AnyAsync(b => b.CageId == cageId);
            if (hasBookings)
                return Json(new { success = false, message = "Chuồng này có lịch sử booking, không thể xóa." });

            _context.Cages.Remove(cage);
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = $"Xóa chuồng {cageId} thành công!" });
        }
    }
}
