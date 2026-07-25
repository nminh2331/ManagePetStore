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
        [HttpPost("MovePetCage")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MovePetCage(int bookingId, string targetCageId)
        {
            if (bookingId <= 0 || string.IsNullOrWhiteSpace(targetCageId))
            {
                return Json(new { success = false, message = "Thông tin chuyển chuồng không hợp lệ." });
            }

            targetCageId = targetCageId.Trim().ToUpperInvariant();
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                var booking = await _context.HotelBookings
                    .Include(b => b.Cage)
                    .Include(b => b.Pet)
                    .FirstOrDefaultAsync(b =>
                        b.HotelBookingId == bookingId &&
                        ActiveHotelStatuses.Contains(b.Status));

                if (booking == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy lượt lưu trú đang hoạt động." });
                }

                string sourceCageId = booking.CageId;
                if (string.Equals(sourceCageId, targetCageId, StringComparison.OrdinalIgnoreCase))
                {
                    return Json(new { success = false, message = "Thú cưng đang ở chuồng này." });
                }

                var targetCage = await _context.Cages
                    .Include(c => c.RoomType)
                    .FirstOrDefaultAsync(c => c.CageId == targetCageId);

                if (targetCage == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy chuồng đích." });
                }

                if (targetCage.Status != "Trống")
                {
                    return Json(new { success = false, message = $"Chuồng {targetCageId} không còn trống." });
                }

                if (!targetCage.RoomType.Status || !HotelRoomTypeCatalog.IsSupported(targetCage.RoomType.Code))
                {
                    return Json(new { success = false, message = "Loại chuồng đích đang ngừng hoạt động." });
                }

                bool targetHasConflict = await _context.HotelBookings.AnyAsync(b =>
                    b.CageId == targetCageId &&
                    b.HotelBookingId != booking.HotelBookingId &&
                    BlockingHotelStatuses.Contains(b.Status) &&
                    (!booking.CheckOutDate.HasValue || b.CheckInDate < booking.CheckOutDate.Value) &&
                    (!b.CheckOutDate.HasValue || b.CheckOutDate.Value > booking.CheckInDate));

                if (targetHasConflict)
                {
                    return Json(new { success = false, message = $"Chuồng {targetCageId} đã có lịch đặt trùng thời gian lưu trú." });
                }

                if (booking.Cage != null)
                {
                    booking.Cage.Status = "Đang dọn dẹp";
                }

                booking.CageId = targetCage.CageId;
                booking.Cage = targetCage;
                targetCage.Status = "Đang dùng";

                var actor = GetCurrentStaffSnapshot();
                DateTime movedAt = DateTime.Now;
                var openSegment = await _context.HotelCageStaySegments
                    .Where(segment => segment.HotelBookingId == booking.HotelBookingId && segment.EndedAt == null)
                    .OrderByDescending(segment => segment.StartedAt)
                    .FirstOrDefaultAsync();
                if (openSegment != null)
                {
                    openSegment.EndedAt = movedAt;
                    openSegment.EndReason = "StaffOperationalMove";
                    await _context.SaveChangesAsync();
                }
                _context.HotelCageStaySegments.Add(new HotelCageStaySegment
                {
                    HotelBookingId = booking.HotelBookingId,
                    CageId = targetCage.CageId,
                    RoomTypeId = targetCage.RoomTypeId,
                    DailyPriceSnapshot = booking.BaseDailyPrice,
                    StartedAt = movedAt,
                    StartReason = "StaffOperationalMove",
                    CreatedAt = movedAt
                });
                _context.RoomMaintenanceLogs.Add(new RoomMaintenanceLog
                {
                    CageId = sourceCageId,
                    PreviousStatus = "Đang dùng",
                    NewStatus = "Đang dọn dẹp",
                    Reason = $"Dọn dẹp sau khi nhân viên chuyển pet sang chuồng {targetCageId}.",
                    Note = "Chuyển chuồng do vận hành, không tính thêm chênh lệch giá.",
                    StartedAt = movedAt,
                    CreatedByUserId = actor.UserId,
                    CreatedByName = actor.Name
                });
                _context.PetBioTimelines.Add(new PetBioTimeline
                {
                    PetId = booking.PetId,
                    HotelBookingId = booking.HotelBookingId,
                    Date = movedAt,
                    Title = "Chuyển chuồng lưu trú",
                    Type = "HotelCageMove",
                    Description = $"Chuyển từ chuồng {sourceCageId} sang {targetCageId} do vận hành; không phát sinh chênh lệch giá. Nhân viên: {actor.Name}."
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Json(new
                {
                    success = true,
                    message = $"Đã chuyển {booking.Pet.Name} từ chuồng {sourceCageId} sang {targetCageId}."
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Không thể chuyển HotelBooking {BookingId} sang chuồng {TargetCageId}.", bookingId, targetCageId);
                return Json(new { success = false, message = "Không thể chuyển chuồng do lỗi hệ thống. Vui lòng thử lại." });
            }
        }


        [HttpPost("ProcessCageChangeRequest")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessCageChangeRequest(int requestId, string decision, string? note)
        {
            decision = decision?.Trim().ToLowerInvariant() ?? string.Empty;
            note = note?.Trim();
            if (requestId <= 0 || decision is not ("approve" or "reject"))
            {
                return Json(new { success = false, message = "Quyết định xử lý yêu cầu không hợp lệ." });
            }
            if (!string.IsNullOrWhiteSpace(note) && note.Length > 1000)
            {
                return Json(new { success = false, message = "Ghi chú xử lý không được vượt quá 1.000 ký tự." });
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                var changeRequest = await _context.HotelCageChangeRequests
                    .Include(request => request.HotelBooking).ThenInclude(booking => booking.Pet)
                    .Include(request => request.HotelBooking).ThenInclude(booking => booking.Customer)
                    .Include(request => request.HotelBooking).ThenInclude(booking => booking.FoodPlan)
                    .Include(request => request.HotelBooking).ThenInclude(booking => booking.CheckoutStatement)
                    .Include(request => request.SourceCage).ThenInclude(cage => cage.RoomType)
                    .Include(request => request.TargetCage).ThenInclude(cage => cage.RoomType)
                    .FirstOrDefaultAsync(request => request.ChangeRequestId == requestId);
                if (changeRequest == null || changeRequest.Status != "Pending")
                {
                    return Json(new { success = false, message = "Yêu cầu không tồn tại hoặc đã được xử lý." });
                }

                var booking = changeRequest.HotelBooking;
                var statusKey = ResolveHotelStatusKey(booking.Status);
                if (statusKey is not ("reserved" or "active") || booking.CheckoutStatement != null)
                {
                    return Json(new { success = false, message = "Booking không còn đủ điều kiện đổi chuồng." });
                }

                var actor = GetCurrentStaffSnapshot();
                var now = DateTime.Now;
                if (decision == "reject")
                {
                    changeRequest.Status = "Rejected";
                    changeRequest.ProcessedAt = now;
                    changeRequest.ProcessedByUserId = actor.UserId;
                    changeRequest.ProcessedByName = actor.Name;
                    changeRequest.DecisionNote = string.IsNullOrWhiteSpace(note) ? "Không đáp ứng điều kiện vận hành tại thời điểm xử lý." : note;
                    _context.PetBioTimelines.Add(new PetBioTimeline
                    {
                        PetId = booking.PetId,
                        HotelBookingId = booking.HotelBookingId,
                        Date = now,
                        Title = "Từ chối yêu cầu đổi chuồng",
                        Type = "CageChangeRejected",
                        Description = $"Yêu cầu đổi từ {changeRequest.SourceCageId} sang {changeRequest.TargetCageId} bị từ chối. " +
                            $"Ghi chú: {changeRequest.DecisionNote}. Nhân viên: {actor.Name}."
                    });
                    AddCageChangeCustomerNotification(changeRequest, false);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    await _hotelEmailService.SendCageChangeDecisionAsync(
                        booking.Customer.Email, booking.Customer.FullName, booking.HotelBookingId, booking.Pet.Name,
                        changeRequest.SourceCageId, changeRequest.TargetCageId, false, 0, changeRequest.DecisionNote);
                    return Json(new { success = true, message = "Đã từ chối yêu cầu đổi chuồng và thông báo cho khách hàng." });
                }

                if (!string.Equals(booking.CageId, changeRequest.SourceCageId, StringComparison.OrdinalIgnoreCase))
                {
                    return Json(new { success = false, message = "Booking đã được chuyển khỏi chuồng nguồn; yêu cầu này không còn hợp lệ." });
                }
                if (changeRequest.TargetCage.Status != "Trống" ||
                    !changeRequest.TargetCage.RoomType.Status ||
                    !HotelRoomTypeCatalog.IsSupported(changeRequest.TargetCage.RoomType.Code))
                {
                    return Json(new { success = false, message = "Chuồng đích không còn ở trạng thái sẵn sàng." });
                }

                var intervalStart = statusKey == "active" ? now : booking.CheckInDate;
                var intervalEnd = booking.ScheduledCheckOutDate
                    ?? booking.CheckOutDate
                    ?? booking.CheckInDate.AddDays(Math.Max(booking.StayDays, 1));
                bool hasConflict = await _context.HotelBookings.AnyAsync(item =>
                    item.HotelBookingId != booking.HotelBookingId &&
                    item.CageId == changeRequest.TargetCageId &&
                    BlockingHotelStatuses.Contains(item.Status) &&
                    item.CheckInDate < intervalEnd &&
                    (!item.CheckOutDate.HasValue || item.CheckOutDate.Value > intervalStart));
                if (hasConflict)
                {
                    return Json(new { success = false, message = "Chuồng đích vừa có lịch đặt trùng; chưa thể duyệt yêu cầu." });
                }

                int remainingDays = statusKey == "reserved"
                    ? Math.Max(booking.StayDays, 1)
                    : Math.Max(1, (int)Math.Ceiling((intervalEnd - now).TotalHours / 24d));
                decimal oldDailyPrice = booking.BaseDailyPrice;
                decimal newDailyPrice = changeRequest.TargetCage.RoomType.DailyPrice;
                decimal discountRate = booking.Subtotal > 0
                    ? Math.Clamp(booking.Discount / booking.Subtotal, 0, 1)
                    : 0;
                decimal oldFinalAmount = booking.FinalAmount;

                if (statusKey == "reserved")
                {
                    booking.Subtotal = newDailyPrice * Math.Max(booking.StayDays, 1);
                    booking.Discount = decimal.Round(booking.Subtotal * discountRate, 0, MidpointRounding.AwayFromZero);
                    booking.FinalAmount = Math.Max(0, booking.Subtotal - booking.Discount + (booking.FoodPlan?.TotalAmount ?? 0));
                }
                else
                {
                    decimal rawDifference = (newDailyPrice - oldDailyPrice) * remainingDays;
                    decimal discountDifference = decimal.Round(rawDifference * discountRate, 0, MidpointRounding.AwayFromZero);
                    booking.Subtotal = Math.Max(0, booking.Subtotal + rawDifference);
                    booking.Discount = Math.Max(0, booking.Discount + discountDifference);
                    booking.FinalAmount = Math.Max(0, booking.FinalAmount + rawDifference - discountDifference);
                }

                decimal appliedDifference = booking.FinalAmount - oldFinalAmount;
                string sourceCageId = booking.CageId;
                booking.CageId = changeRequest.TargetCageId;
                booking.BaseDailyPrice = newDailyPrice;

                if (statusKey == "active")
                {
                    changeRequest.SourceCage.Status = "Đang dọn dẹp";
                    changeRequest.TargetCage.Status = "Đang dùng";
                    var openSegment = await _context.HotelCageStaySegments
                        .Where(segment => segment.HotelBookingId == booking.HotelBookingId && segment.EndedAt == null)
                        .OrderByDescending(segment => segment.StartedAt)
                        .FirstOrDefaultAsync();
                    if (openSegment != null)
                    {
                        openSegment.EndedAt = now;
                        openSegment.EndReason = "CageChange";
                        await _context.SaveChangesAsync();
                    }
                    _context.HotelCageStaySegments.Add(new HotelCageStaySegment
                    {
                        HotelBookingId = booking.HotelBookingId,
                        CageId = changeRequest.TargetCageId,
                        RoomTypeId = changeRequest.TargetCage.RoomTypeId,
                        DailyPriceSnapshot = newDailyPrice,
                        StartedAt = now,
                        StartReason = "CageChange",
                        CreatedAt = now
                    });
                    _context.RoomMaintenanceLogs.Add(new RoomMaintenanceLog
                    {
                        CageId = sourceCageId,
                        PreviousStatus = "Đang dùng",
                        NewStatus = "Đang dọn dẹp",
                        Reason = $"Dọn dẹp sau khi chuyển pet sang chuồng {changeRequest.TargetCageId}.",
                        Note = $"Theo yêu cầu đổi chuồng #{changeRequest.ChangeRequestId}.",
                        StartedAt = now,
                        CreatedByUserId = actor.UserId,
                        CreatedByName = actor.Name
                    });
                }

                changeRequest.Status = "Approved";
                changeRequest.RemainingDaysSnapshot = remainingDays;
                changeRequest.SourceDailyPriceSnapshot = oldDailyPrice;
                changeRequest.TargetDailyPriceSnapshot = newDailyPrice;
                changeRequest.PriceDifferenceSnapshot = appliedDifference;
                changeRequest.ProcessedAt = now;
                changeRequest.ProcessedByUserId = actor.UserId;
                changeRequest.ProcessedByName = actor.Name;
                changeRequest.DecisionNote = string.IsNullOrWhiteSpace(note) ? "Đã kiểm tra chuồng đích và lịch đặt trùng." : note;
                changeRequest.AppliedAt = now;

                _context.PetBioTimelines.Add(new PetBioTimeline
                {
                    PetId = booking.PetId,
                    HotelBookingId = booking.HotelBookingId,
                    Date = now,
                    Title = "Duyệt đổi chuồng",
                    Type = "HotelCageMove",
                    Description = $"Chuyển từ chuồng {sourceCageId} sang {changeRequest.TargetCageId}; " +
                        $"tính chênh lệch {appliedDifference:N0}đ cho {remainingDays} ngày còn lại. Nhân viên: {actor.Name}."
                });
                AddCageChangeCustomerNotification(changeRequest, true);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                await _hotelEmailService.SendCageChangeDecisionAsync(
                    booking.Customer.Email, booking.Customer.FullName, booking.HotelBookingId, booking.Pet.Name,
                    sourceCageId, changeRequest.TargetCageId, true, appliedDifference, changeRequest.DecisionNote);
                return Json(new
                {
                    success = true,
                    message = $"Đã chuyển {booking.Pet.Name} sang {changeRequest.TargetCageId}; chênh lệch {appliedDifference:N0}đ."
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Cannot process Hotel cage change request {RequestId}.", requestId);
                return Json(new { success = false, message = "Không thể xử lý yêu cầu đổi chuồng do lỗi hệ thống." });
            }
        }

        private void AddCageChangeCustomerNotification(HotelCageChangeRequest request, bool approved)
        {
            var difference = request.PriceDifferenceSnapshot;
            string priceMessage = difference > 0
                ? $" Phụ thu {difference:N0}đ."
                : difference < 0
                    ? $" Giảm trừ {Math.Abs(difference):N0}đ."
                    : " Không phát sinh chênh lệch giá.";
            _context.CustomerNotifications.Add(new CustomerNotification
            {
                CustomerId = request.CustomerId,
                HotelBookingId = request.HotelBookingId,
                Type = approved ? "CageChangeApproved" : "CageChangeRejected",
                Title = approved ? "Yêu cầu đổi chuồng đã được duyệt" : "Yêu cầu đổi chuồng bị từ chối",
                Message = approved
                    ? $"Đã chuyển từ {request.SourceCageId} sang {request.TargetCageId}.{priceMessage}"
                    : $"Yêu cầu đổi từ {request.SourceCageId} sang {request.TargetCageId} bị từ chối. {request.DecisionNote}",
                LinkUrl = $"/Customer/HotelBooking/Details/{request.HotelBookingId}",
                CreatedAt = DateTime.Now
            });
        }

    }
}
