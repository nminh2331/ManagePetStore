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
        // [nam] Hoàn tất trả pet sau khi bảng kê Hotel đã được thanh toán hợp lệ.
        [HttpPost("CheckOut")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckOut(int bookingId)
        {
            // [nam][Flow] Tải đồng thời bảng kê và Order để quyết định trả pet dựa trên trạng thái thanh toán thật.
            var booking = await _context.HotelBookings
                .Include(b => b.Cage)
                .Include(b => b.Pet)
                .Include(b => b.Customer)
                .Include(b => b.CheckoutStatement)
                    .ThenInclude(statement => statement!.Order)
                .FirstOrDefaultAsync(b => b.HotelBookingId == bookingId);

            if (booking == null)
            {
                return Json(new { success = false, message = "Không tìm thấy booking." });
            }

            // [nam][BR] Chỉ pet đang ở mới được trả; thao tác lặp sau khi đã trả bị chặn bằng trạng thái booking.
            if (!ActiveHotelStatuses.Contains(booking.Status))
            {
                return Json(new { success = false, message = "Chỉ có thể trả chuồng cho lượt lưu trú đang hoạt động." });
            }

            var checkout = booking.CheckoutStatement;
            if (checkout?.OrderId == null)
            {
                return Json(new { success = false, message = "Booking chưa được chốt chi phí hoặc chưa được thu ngân tạo hóa đơn." });
            }

            // [nam][BR] Có OrderId chưa đủ, hóa đơn liên kết phải đạt trạng thái thanh toán được phép finalize.
            if (!HotelCheckoutWorkflow.CanFinalize(checkout.OrderId, checkout.Order?.Status))
            {
                return Json(new { success = false, message = "Hóa đơn lưu trú chuồng chưa thanh toán thành công." });
            }

            // [nam][Flow] CheckoutAt đã chốt trong bảng kê là mốc trả thực tế dùng thống nhất cho lịch sử và tính tiền.
            booking.Status = "Đã trả";
            booking.ScheduledCheckInDate ??= booking.CheckInDate;
            booking.ScheduledCheckOutDate ??= booking.CheckOutDate;
            booking.ActualCheckInAt ??= booking.CheckInDate;
            booking.ActualCheckOutAt = checkout.CheckoutAt;
            booking.CheckOutDate = booking.ActualCheckOutAt;

            var staff = GetCurrentStaffSnapshot();
            _context.PetBioTimelines.Add(new PetBioTimeline
            {
                PetId = booking.PetId,
                HotelBookingId = booking.HotelBookingId,
                Date = booking.ActualCheckOutAt.Value,
                Title = "Hoàn tất lưu trú",
                Type = "HotelCheckOut",
                Description = $"Thú cưng được trả cho chủ nuôi. Chuồng cuối cùng: {booking.CageId}. Nhân viên: {staff.Name}."
            });

            // [nam][BR] Sau khi trả pet, chuồng phải qua trạng thái dọn dẹp; không mở trống trực tiếp.
            if (booking.Cage != null)
            {
                var previousCageStatus = booking.Cage.Status;
                booking.Cage.Status = "Đang dọn dẹp";
                _context.RoomMaintenanceLogs.Add(new RoomMaintenanceLog
                {
                    CageId = booking.CageId,
                    PreviousStatus = previousCageStatus,
                    NewStatus = "Đang dọn dẹp",
                    Reason = $"Dọn dẹp sau khi hoàn tất booking HB{booking.HotelBookingId:0000}.",
                    Note = $"Pet {booking.Pet.Name} đã trả cho chủ nuôi.",
                    StartedAt = booking.ActualCheckOutAt.Value,
                    CreatedByUserId = staff.UserId,
                    CreatedByName = staff.Name
                });
            }

            // [nam][Flow] Đóng phân đoạn chuồng cuối cùng để lịch sử không còn một khoảng lưu trú mở.
            var openStaySegment = await _context.HotelCageStaySegments
                .Where(segment => segment.HotelBookingId == booking.HotelBookingId && segment.EndedAt == null)
                .OrderByDescending(segment => segment.StartedAt)
                .FirstOrDefaultAsync();
            if (openStaySegment != null)
            {
                openStaySegment.EndedAt = booking.ActualCheckOutAt;
                openStaySegment.EndReason = "CheckOut";
            }

            checkout.Status = "Paid";
            checkout.PaidAt = DateTime.Now;

            await _context.SaveChangesAsync();

            // [nam][Flow] Chỉ gửi email sau khi trạng thái pet, chuồng và bảng kê đã lưu thành công.
            await _hotelEmailService.SendCheckOutAsync(
                booking.Customer.Email,
                booking.Customer.FullName,
                booking.HotelBookingId,
                booking.Pet.Name,
                booking.CageId,
                booking.ActualCheckOutAt.Value,
                checkout.TotalAmount);

            return Json(new { success = true, message = $"Đã hoàn tất trả {booking.Pet?.Name ?? "thú cưng"}; chuồng chuyển sang chờ dọn dẹp." });
        }

        // [nam] Mở lại bảng kê Hotel khi đơn thanh toán liên kết đã bị hủy.
        [HttpPost("ResetHotelCheckout")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetHotelCheckout(int bookingId)
        {
            var booking = await _context.HotelBookings
                .Include(item => item.Pet)
                .Include(item => item.CheckoutStatement)
                    .ThenInclude(statement => statement!.Items)
                .Include(item => item.CheckoutStatement)
                    .ThenInclude(statement => statement!.Order)
                .FirstOrDefaultAsync(item => item.HotelBookingId == bookingId);

            if (booking == null || !ActiveHotelStatuses.Contains(booking.Status))
            {
                return Json(new { success = false, message = "Không tìm thấy lượt lưu trú đang hoạt động." });
            }

            var statement = booking.CheckoutStatement;
            if (statement == null || statement.Status == "Draft")
            {
                return Json(new { success = true, message = "Lượt lưu trú đã sẵn sàng chốt lại chi phí." });
            }

            // [nam][BR] Chỉ thu hồi khi chưa tạo hóa đơn hoặc hóa đơn đã hủy; đơn đang xử lý/đã trả tiền bị khóa.
            if (!HotelCheckoutWorkflow.CanReset(statement))
            {
                return Json(new
                {
                    success = false,
                    message = "Bảng kê đã gắn với hóa đơn đang xử lý. Chỉ có thể thu hồi khi chưa tạo hóa đơn hoặc hóa đơn đã hủy."
                });
            }

            _context.HotelCheckoutItems.RemoveRange(statement.Items);
            statement.Status = "Draft";
            statement.OrderId = null;
            statement.PaidAt = null;

            var staff = GetCurrentStaffSnapshot();
            _context.PetBioTimelines.Add(new PetBioTimeline
            {
                PetId = booking.PetId,
                HotelBookingId = booking.HotelBookingId,
                Date = DateTime.Now,
                Title = "Thu hồi bảng kê",
                Type = "HotelCheckoutReset",
                Description = $"Nhân viên {staff.Name} đã thu hồi bảng kê để kiểm tra và gửi lại quầy thu ngân."
            });

            await _context.SaveChangesAsync();
            return Json(new
            {
                success = true,
                message = $"Đã thu hồi bảng kê của {booking.Pet?.Name ?? "thú cưng"}. Có thể thực hiện trả chuồng lại."
            });
        }

        // [nam] Trả về bảng tính chi phí Hotel tạm thời để Staff kiểm tra trước khi chốt.
        [HttpGet("HotelCheckoutPreview/{bookingId:int}")]
        public async Task<IActionResult> HotelCheckoutPreview(int bookingId)
        {
            try
            {
                var preview = await _hotelCheckoutService.GetPreviewAsync(bookingId);
                return preview == null
                    ? Json(new { success = false, message = "Không tìm thấy lượt đặt chuồng." })
                    : Json(new { success = true, data = preview });
            }
            catch (InvalidOperationException ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // [nam] Chốt chi phí lưu trú và gửi bảng kê sang quầy thu ngân.
        [HttpPost("PrepareHotelCheckout")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PrepareHotelCheckout(PrepareHotelCheckoutRequest request)
        {
            // [nam][Validate] ModelState kiểm tra giới hạn tiền/mô tả trước khi service đối soát lại dữ liệu booking.
            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = "Chi phí phát sinh không hợp lệ." });
            }

            try
            {
                var staff = GetCurrentStaffSnapshot();
                var preview = await _hotelCheckoutService.PrepareAsync(request, staff.UserId, staff.Name);
                return Json(new { success = true, message = "Đã chốt chi phí và gửi sang quầy thu ngân.", data = preview });
            }
            catch (InvalidOperationException ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // [nam] Hủy booking Hotel online chưa được tiếp nhận và hoàn tài nguyên đã giữ.
        [HttpPost("CancelOnlineHotelBooking")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelOnlineHotelBooking(int bookingId)
        {
            var booking = await _context.HotelBookings
                .Include(b => b.Pet)
                .Include(b => b.FoodPlan)
                .FirstOrDefaultAsync(b => b.HotelBookingId == bookingId);

            if (booking == null)
            {
                return Json(new { success = false, message = "Không tìm thấy lịch đặt online." });
            }

            // [nam][BR] Staff chỉ hủy booking online chưa check-in; booking đang ở phải đi qua checkout.
            if (!string.Equals(booking.Status, "Đã đặt", StringComparison.OrdinalIgnoreCase))
            {
                return Json(new { success = false, message = "Chỉ có thể hủy lịch đặt online đang chờ tiếp nhận." });
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                // [nam][Flow] Hoàn đúng số suất đã giữ và đặt về 0 để không hoàn kho hai lần khi request lặp.
                if (booking.FoodPlan?.ProductSku != null && booking.FoodPlan.InventoryQuantityDeducted > 0)
                {
                    await _inventoryBatchService.RestockToBatches(
                        booking.FoodPlan.ProductSku,
                        booking.FoodPlan.InventoryQuantityDeducted);
                    booking.FoodPlan.InventoryQuantityDeducted = 0;
                }

                booking.Status = "Đã hủy";
                var staff = GetCurrentStaffSnapshot();
                _context.PetBioTimelines.Add(new PetBioTimeline
                {
                    PetId = booking.PetId,
                    HotelBookingId = booking.HotelBookingId,
                    Date = DateTime.Now,
                    Title = "Hủy lịch lưu trú",
                    Type = "HotelBookingCancelled",
                    Description = $"Lịch đặt online được hủy bởi {staff.Name}; suất ăn đã giữ được hoàn lại kho."
                });
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (ManagePetStore.Exceptions.ServiceException ex)
            {
                await transaction.RollbackAsync();
                return Json(new { success = false, message = ex.Message });
            }

            return Json(new
            {
                success = true,
                message = $"Đã hủy lịch đặt online của {booking.Pet?.Name ?? "thú cưng"}."
            });
        }

    }
}
